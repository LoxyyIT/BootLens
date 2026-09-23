using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Win32;
using BootLens.Core.Domain;
using BootLens.Core.Services;

namespace BootLens.Windows;

public sealed class WindowsStartupScanner : IStartupScanner, IScanCoverageProvider
{
    private static readonly ConcurrentDictionary<string, CachedInspection> InspectionCache = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ScanSourceCoverage> LastCoverage { get; private set; } = [];
    private static readonly (RegistryHive Hive, string Path, StartupMechanism Mechanism, RegistryView View)[] RunLocations =
    [
        (RegistryHive.CurrentUser, "Software\\Microsoft\\Windows\\CurrentVersion\\Run", StartupMechanism.RegistryRun, RegistryView.Registry64),
        (RegistryHive.CurrentUser, "Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce", StartupMechanism.RegistryRunOnce, RegistryView.Registry64),
        (RegistryHive.CurrentUser, "Software\\Microsoft\\Windows\\CurrentVersion\\Run", StartupMechanism.RegistryRun, RegistryView.Registry32),
        (RegistryHive.CurrentUser, "Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce", StartupMechanism.RegistryRunOnce, RegistryView.Registry32),
        (RegistryHive.LocalMachine, "Software\\Microsoft\\Windows\\CurrentVersion\\Run", StartupMechanism.RegistryRun, RegistryView.Registry64),
        (RegistryHive.LocalMachine, "Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce", StartupMechanism.RegistryRunOnce, RegistryView.Registry64),
        (RegistryHive.LocalMachine, "Software\\Microsoft\\Windows\\CurrentVersion\\Run", StartupMechanism.RegistryRun, RegistryView.Registry32),
        (RegistryHive.LocalMachine, "Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce", StartupMechanism.RegistryRunOnce, RegistryView.Registry32),
        (RegistryHive.LocalMachine, "Software\\Microsoft\\Windows\\CurrentVersion\\RunServices", StartupMechanism.RegistryRun, RegistryView.Registry64),
        (RegistryHive.LocalMachine, "Software\\Microsoft\\Windows\\CurrentVersion\\RunServicesOnce", StartupMechanism.RegistryRunOnce, RegistryView.Registry64),
        (RegistryHive.LocalMachine, "Software\\Microsoft\\Windows\\CurrentVersion\\RunServices", StartupMechanism.RegistryRun, RegistryView.Registry32),
        (RegistryHive.LocalMachine, "Software\\Microsoft\\Windows\\CurrentVersion\\RunServicesOnce", StartupMechanism.RegistryRunOnce, RegistryView.Registry32)
    ];

    public Task<IReadOnlyList<StartupEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var entries = new List<StartupEntry>();
            foreach (var location in RunLocations) ScanRegistry(entries, location.Hive, location.Path, location.Mechanism, location.View, cancellationToken);
            ScanDisabledRegistry(entries, cancellationToken);
            ScanWindowsRegistrySurfaces(entries, cancellationToken);
            ScanStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Current user", cancellationToken);
            ScanStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "All users", cancellationToken);
            ScanServices(entries, cancellationToken);
            ScanScheduledTasks(entries, cancellationToken);
            var wmi = ScanWmi(entries, cancellationToken);
            var results = entries.GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            LastCoverage = BuildCoverage(results, wmi);
            return (IReadOnlyList<StartupEntry>)results;
        }, cancellationToken);
    }

    private static WmiScanResult ScanWmi(List<StartupEntry> entries, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return new(false, 0, 0);
        const string script = "$ProgressPreference='SilentlyContinue'; Get-CimInstance -ClassName Win32_StartupCommand -ErrorAction Stop | Select-Object Name,Command,Location,User | ConvertTo-Json -Compress -Depth 3";
        try
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoLogo -NoProfile -NonInteractive -Command \"{script}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.Unicode
            });
            if (process is null) return new(false, 0, 0);
            var outputTask = process.StandardOutput.ReadToEndAsync(token);
            var errorTask = process.StandardError.ReadToEndAsync(token);
            if (!process.WaitForExit(15000))
            {
                process.Kill(true);
                return new(false, 0, 0);
            }
            token.ThrowIfCancellationRequested();
            var output = outputTask.GetAwaiter().GetResult();
            _ = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output)) return new(process.ExitCode == 0, 0, 0);
            using var document = JsonDocument.Parse(output);
            if (document.RootElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return new(true, 0, 0);
            var rows = document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.EnumerateArray().ToArray() : [document.RootElement];
            var added = 0;
            foreach (var row in rows)
            {
                token.ThrowIfCancellationRequested();
                var name = ReadJsonString(row, "Name");
                var command = ReadJsonString(row, "Command");
                var location = ReadJsonString(row, "Location");
                var user = ReadJsonString(row, "User");
                if (string.IsNullOrWhiteSpace(command)) continue;
                var path = ExtractExecutablePath(command);
                if (entries.Any(entry => SameStartupCommand(entry, path, command))) continue;
                var entry = CreateEntry(string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path ?? command) : name, command, StartupMechanism.Wmi, $"WMI · {location}", $"{location}:{name}:{user}", false) with
                {
                    Trigger = user,
                    Description = string.IsNullOrWhiteSpace(location) ? "Win32_StartupCommand" : location
                };
                entries.Add(entry);
                added++;
            }
            return new(true, rows.Length, added);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) when (OperatingSystem.IsWindows()) { return new(false, 0, 0); }
    }

    private static bool SameStartupCommand(StartupEntry entry, string? path, string command)
    {
        if (string.Equals(entry.CommandLine?.Trim(), command.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return !string.IsNullOrWhiteSpace(path) && string.Equals(entry.ExecutablePath?.Trim().Trim('"'), path.Trim().Trim('"'), StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadJsonString(JsonElement row, string property) => row.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString() : null;

    private static IReadOnlyList<ScanSourceCoverage> BuildCoverage(IReadOnlyCollection<StartupEntry> entries, WmiScanResult wmi)
    {
        var definitions = new (string Key, StartupMechanism[] Mechanisms, bool Supported, string Status)[]
        {
            ("Logon", [StartupMechanism.RegistryRun, StartupMechanism.RegistryRunOnce, StartupMechanism.StartupFolder], true, "Implemented"),
            ("Services", [StartupMechanism.Service], true, "Implemented"),
            ("Drivers", [StartupMechanism.Driver], true, "Implemented"),
            ("ScheduledTasks", [StartupMechanism.ScheduledTask], true, "Implemented"),
            ("Winlogon", [StartupMechanism.Winlogon], true, "Implemented"),
            ("Explorer", [StartupMechanism.Explorer, StartupMechanism.Shell], true, "Implemented"),
            ("Wmi", [StartupMechanism.Wmi], wmi.Succeeded, wmi.Succeeded ? "Available" : "Unavailable"),
            ("BootExecute", [StartupMechanism.BootExecute], true, "Implemented"),
            ("AppInit", [StartupMechanism.AppInit], true, "Implemented"),
            ("KnownDll", [StartupMechanism.KnownDll], true, "Implemented"),
            ("Codecs", [StartupMechanism.Codecs], true, "Implemented"),
            ("ImageHijack", [StartupMechanism.ImageHijack], true, "Implemented"),
            ("Winsock", [StartupMechanism.Winsock], true, "Implemented"),
            ("PrintMonitor", [StartupMechanism.PrintMonitor], true, "Implemented"),
            ("LsaProvider", [StartupMechanism.LsaProvider], true, "Implemented"),
            ("NetworkProvider", [StartupMechanism.NetworkProvider], true, "Implemented"),
            ("Office", [StartupMechanism.Office], false, "Unsupported"),
            ("PackagedApp", [StartupMechanism.PackagedApp], false, "Unsupported"),
            ("InternetExplorer", [], false, "Unsupported"),
            ("PrintProcessors", [], false, "Unsupported"),
            ("BootVerification", [], false, "Unsupported"),
            ("SidebarGadgets", [], false, "Unsupported")
        };
        return definitions.Select(item => new ScanSourceCoverage(item.Key, entries.Count(entry => item.Mechanisms.Contains(entry.Mechanism)), item.Supported, item.Status, item.Key == "Wmi" ? $"{wmi.RawCount}:{wmi.AddedCount}" : null)).ToArray();
    }

    private sealed record WmiScanResult(bool Succeeded, int RawCount, int AddedCount);

    private static void ScanRegistry(List<StartupEntry> entries, RegistryHive hive, string path, StartupMechanism mechanism, RegistryView view, CancellationToken token)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.OpenSubKey(path);
        if (key is null) return;
        foreach (var valueName in key.GetValueNames())
        {
            token.ThrowIfCancellationRequested();
            var command = key.GetValue(valueName)?.ToString();
            if (string.IsNullOrWhiteSpace(command)) continue;
            entries.Add(CreateEntry(valueName, command, mechanism, FormatRegistrySource(hive, path, view), valueName, isDisabled: false));
        }
    }

    private static void ScanWindowsRegistrySurfaces(List<StartupEntry> entries, CancellationToken token)
    {
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SYSTEM\\CurrentControlSet\\Control\\Session Manager", StartupMechanism.BootExecute, token, "BootExecute");
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon", StartupMechanism.Winlogon, token, "Shell", "Userinit");
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Windows", StartupMechanism.AppInit, token, "AppInit_DLLs");
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\KnownDLLs", StartupMechanism.KnownDll, token);
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ShellExecuteHooks", StartupMechanism.Explorer, token, recursive: true);
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Drivers32", StartupMechanism.Codecs, token);
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options", StartupMechanism.ImageHijack, token, recursive: true);
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SYSTEM\\CurrentControlSet\\Services\\WinSock2\\Parameters\\Protocol_Catalog9", StartupMechanism.Winsock, token, recursive: true);
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SYSTEM\\CurrentControlSet\\Control\\Print\\Monitors", StartupMechanism.PrintMonitor, token, recursive: true);
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SYSTEM\\CurrentControlSet\\Control\\Lsa", StartupMechanism.LsaProvider, token, "Authentication Packages", "Security Packages", "Notification Packages");
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SYSTEM\\CurrentControlSet\\Control\\NetworkProvider\\Order", StartupMechanism.NetworkProvider, token, "ProviderOrder");
        ScanRegistryValues(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Windows", StartupMechanism.Shell, token, "Load");
        ScanRegistryValues(entries, RegistryHive.CurrentUser, RegistryView.Registry64, "Software\\Microsoft\\Windows NT\\CurrentVersion\\Windows", StartupMechanism.Shell, token, "Load");
    }

    private static void ScanRegistryValues(List<StartupEntry> entries, RegistryHive hive, RegistryView view, string path, StartupMechanism mechanism, CancellationToken token, params string[] valueNames)
    {
        ScanRegistryValues(entries, hive, view, path, mechanism, token, false, valueNames);
    }

    private static void ScanRegistryValues(List<StartupEntry> entries, RegistryHive hive, RegistryView view, string path, StartupMechanism mechanism, CancellationToken token, bool recursive, params string[] valueNames)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path);
            if (key is null) return;
            var targets = recursive ? EnumerateKeys(key).ToArray() : [key];
            foreach (var target in targets)
            {
                var names = valueNames.Length == 0 ? target.GetValueNames() : valueNames;
                foreach (var name in names)
                {
                    token.ThrowIfCancellationRequested();
                    var command = target.GetValue(name)?.ToString();
                    if (string.IsNullOrWhiteSpace(command)) continue;
                    var displayName = $"{target.Name.Split('\\').Last()} · {name}";
                    entries.Add(IsAdvancedSurface(mechanism) ? CreateSurfaceEntry(displayName, command, mechanism, target.Name, name) : CreateEntry(displayName, command, mechanism, target.Name, name, false));
                }
            }
            foreach (var target in targets.Skip(1)) target.Dispose();
        }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
        catch (IOException) { }
    }

    private static IEnumerable<RegistryKey> EnumerateKeys(RegistryKey root)
    {
        yield return root;
        foreach (var childName in root.GetSubKeyNames())
        {
            var child = root.OpenSubKey(childName);
            if (child is null) continue;
            foreach (var nested in EnumerateKeys(child)) yield return nested;
        }
    }

    private static bool IsAdvancedSurface(StartupMechanism mechanism) => mechanism is not (StartupMechanism.RegistryRun or StartupMechanism.RegistryRunOnce or StartupMechanism.StartupFolder or StartupMechanism.Service or StartupMechanism.ScheduledTask);

    private static string FormatRegistrySource(RegistryHive hive, string path, RegistryView view) => $"{view}:{hive}\\{path}";

    private static StartupEntry CreateSurfaceEntry(string name, string value, StartupMechanism mechanism, string source, string identifier)
    {
        var path = ExtractExecutablePath(value);
        var file = InspectFile(path);
        return new StartupEntry { Id = $"{mechanism}:{identifier}:{source}", DisplayName = name, Mechanism = mechanism, State = StartupState.Protected, Publisher = file.Publisher, Description = value, ExecutablePath = path, CommandLine = value, Identifier = identifier, Version = file.Version, Sha256 = file.Sha256, IsSigned = file.IsSigned, SignatureStatus = file.SignatureStatus, SignatureDetail = file.SignatureDetail, IsMicrosoft = file.IsMicrosoft, IsCritical = true, IsBroken = false, SourceLocation = source };
    }

    private static void ScanDisabledRegistry(List<StartupEntry> entries, CancellationToken token)
    {
        using var key = Registry.CurrentUser.OpenSubKey("Software\\BootLens\\DisabledStartup");
        if (key is null) return;
        foreach (var childName in key.GetSubKeyNames())
        {
            token.ThrowIfCancellationRequested();
            using var child = key.OpenSubKey(childName);
            var command = child?.GetValue("Command")?.ToString();
            var mechanismText = child?.GetValue("Mechanism")?.ToString();
            if (string.IsNullOrWhiteSpace(command) || !Enum.TryParse<StartupMechanism>(mechanismText, out var mechanism)) continue;
            if (mechanism is not (StartupMechanism.RegistryRun or StartupMechanism.RegistryRunOnce or StartupMechanism.StartupFolder)) continue;
            var displayName = child?.GetValue("DisplayName")?.ToString() ?? childName;
            var identifier = child?.GetValue("Identifier")?.ToString() ?? childName;
            var source = child?.GetValue("Source")?.ToString() ?? "HKCU\\Software\\BootLens\\DisabledStartup";
            entries.Add(CreateEntry(displayName, command, mechanism, source, identifier, isDisabled: true));
        }
    }

    private static void ScanStartupFolder(List<StartupEntry> entries, string folder, string source, CancellationToken token)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            token.ThrowIfCancellationRequested();
            if (file.EndsWith(".bootlens-disabled", StringComparison.OrdinalIgnoreCase)) continue;
            entries.Add(CreateEntry(Path.GetFileNameWithoutExtension(file), file, StartupMechanism.StartupFolder, folder, file, false));
        }
    }

    private static void ScanServices(List<StartupEntry> entries, CancellationToken token)
    {
        try
        {
            foreach (var service in System.ServiceProcess.ServiceController.GetServices())
            {
                token.ThrowIfCancellationRequested();
                var startMode = TryGetServiceStartMode(service.ServiceName);
                if (startMode is not ("Auto" or "Automatic" or "Delayed" or "Manual" or "Disabled")) continue;
                var command = TryGetServiceImagePath(service.ServiceName);
                var path = ExtractExecutablePath(command ?? string.Empty);
                var file = InspectFile(path);
                var mechanism = TryGetServiceType(service.ServiceName) is 1 or 2 ? StartupMechanism.Driver : StartupMechanism.Service;
                var critical = service.ServiceName.Contains("defender", StringComparison.OrdinalIgnoreCase) || service.ServiceName.Contains("security", StringComparison.OrdinalIgnoreCase) || service.ServiceName.Contains("network", StringComparison.OrdinalIgnoreCase);
                var microsoft = file.IsMicrosoft || path?.Contains(@"\Windows\System32\", StringComparison.OrdinalIgnoreCase) == true;
                entries.Add(new StartupEntry { Id = $"service:{service.ServiceName}", DisplayName = service.DisplayName, Mechanism = mechanism, State = startMode == "Disabled" ? StartupState.Disabled : critical ? StartupState.Protected : StartupState.Enabled, Publisher = file.Publisher, Description = service.Status.ToString(), ExecutablePath = path ?? command ?? service.ServiceName, CommandLine = command ?? service.ServiceName, Identifier = service.ServiceName, Version = file.Version, Sha256 = file.Sha256, IsSigned = file.IsSigned, SignatureStatus = file.SignatureStatus, SignatureDetail = file.SignatureDetail, IsMicrosoft = microsoft, IsCritical = critical, IsBroken = path is not null && file.IsBroken, Trigger = startMode, SourceLocation = "Service Control Manager" });
            }
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
        }
    }

    private static string? TryGetServiceStartMode(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{serviceName}");
        var start = key?.GetValue("Start") as int?;
        return start switch { 2 => "Auto", 3 => "Manual", 4 => "Disabled", _ => null };
    }

    private static string? TryGetServiceImagePath(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{serviceName}");
        return key?.GetValue("ImagePath")?.ToString();
    }

    private static int? TryGetServiceType(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{serviceName}");
        return key?.GetValue("Type") as int?;
    }

    private static void ScanScheduledTasks(List<StartupEntry> entries, CancellationToken token)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe", "/Query /FO CSV /V") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.Unicode };
        try
        {
            using var process = Process.Start(startInfo);
            if (process is null) return;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            foreach (var row in output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                token.ThrowIfCancellationRequested();
                var columns = ParseCsv(row);
                if (columns.Count < 3) continue;
                var taskName = columns.Count > 1 ? columns[1].Trim('"') : string.Empty;
                var taskToRun = columns.Count > 8 && columns[8].Contains(".exe", StringComparison.OrdinalIgnoreCase) ? columns[8].Trim('"') : columns.FirstOrDefault(column => column.Contains("\\", StringComparison.Ordinal) && column.Contains(".exe", StringComparison.OrdinalIgnoreCase))?.Trim('"');
                if (string.IsNullOrWhiteSpace(taskName)) continue;
                var taskStatus = string.Join(" ", columns.Skip(3).Take(9));
                var disabled = ContainsDisabled(taskStatus);
                entries.Add(new StartupEntry { Id = $"task:{taskName}", DisplayName = taskName.TrimStart('\\'), Mechanism = StartupMechanism.ScheduledTask, State = disabled ? StartupState.Disabled : StartupState.Enabled, Identifier = taskName, CommandLine = taskToRun, Trigger = "Task Scheduler", SourceLocation = "Task Scheduler" });
            }
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
        }
    }

    private static bool ContainsDisabled(string output) => output.Contains("Disabled", StringComparison.OrdinalIgnoreCase) || output.Contains("Disabilit", StringComparison.OrdinalIgnoreCase);

    private static List<string> ParseCsv(string row)
    {
        var columns = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var character in row)
        {
            if (character == '"') quoted = !quoted;
            else if (character == ',' && !quoted) { columns.Add(current.ToString()); current.Clear(); }
            else current.Append(character);
        }
        columns.Add(current.ToString());
        return columns;
    }

    private static StartupEntry CreateEntry(string name, string command, StartupMechanism mechanism, string source, string identifier, bool isDisabled)
    {
        var path = ExtractExecutablePath(command);
        var file = InspectFile(path);
        var id = $"{mechanism}:{identifier}:{source}:{path ?? command}";
        return new StartupEntry { Id = id, DisplayName = name, Mechanism = mechanism, State = isDisabled ? StartupState.Disabled : file.IsCritical ? StartupState.Protected : file.IsBroken ? StartupState.Broken : StartupState.Enabled, Publisher = file.Publisher, Description = file.Description, ExecutablePath = path, CommandLine = command, Identifier = identifier, Version = file.Version, Sha256 = file.Sha256, IsSigned = file.IsSigned, SignatureStatus = file.SignatureStatus, SignatureDetail = file.SignatureDetail, IsMicrosoft = file.IsMicrosoft, IsCritical = file.IsCritical, IsBroken = file.IsBroken, SourceLocation = source };
    }

    public static string? ExtractExecutablePath(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.Length == 0) return null;
        if (trimmed[0] == '"')
        {
            var end = trimmed.IndexOf('"', 1);
            return end > 1 ? NormalizePath(Environment.ExpandEnvironmentVariables(trimmed[1..end])) : null;
        }
        var extensionIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (extensionIndex >= 0) return NormalizePath(Environment.ExpandEnvironmentVariables(trimmed[..(extensionIndex + 4)].Trim()));
        return File.Exists(trimmed) ? NormalizePath(Environment.ExpandEnvironmentVariables(trimmed)) : null;
    }

    private static string NormalizePath(string path)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase)) return Path.Combine(windows, path[12..]);
        if (path.StartsWith("System32\\", StringComparison.OrdinalIgnoreCase)) return Path.Combine(windows, path);
        return path;
    }

    private static FileInspection InspectFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return new FileInspection(null, null, null, null, false, SignatureStatus.Unavailable, "The startup entry has no resolved executable path.", false, false, false);
        if (!File.Exists(path)) return new FileInspection(path, null, null, null, false, SignatureStatus.Unavailable, "The configured file is missing.", false, false, true);
        try
        {
            var fileInfo = new FileInfo(path);
            if (InspectionCache.TryGetValue(path, out var cached) && cached.Length == fileInfo.Length && cached.LastWriteUtcTicks == fileInfo.LastWriteTimeUtc.Ticks) return cached.Result;
            var info = FileVersionInfo.GetVersionInfo(path);
            using var stream = File.OpenRead(path);
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            var signature = AuthenticodeVerifier.Verify(path);
            var publisher = signature.Publisher ?? info.CompanyName;
            var microsoft = publisher?.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) == true;
            var result = new FileInspection(info.FileName, publisher, info.FileDescription, info.FileVersion, signature.IsValid, signature.Status, signature.Detail, microsoft, microsoft, false) with { Sha256 = hash };
            InspectionCache[path] = new CachedInspection(fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks, result);
            return result;
        }
        catch (IOException) { return new FileInspection(path, null, null, null, false, SignatureStatus.Unavailable, "The file could not be read.", false, false, !File.Exists(path)); }
        catch (UnauthorizedAccessException) { return new FileInspection(path, null, null, null, false, SignatureStatus.Unavailable, "Access to the file was denied.", false, false, false); }
        catch (SecurityException) { return new FileInspection(path, null, null, null, false, SignatureStatus.Unavailable, "Windows denied access to the file.", false, false, false); }
    }

    private sealed record FileInspection(string? Path, string? Publisher, string? Description, string? Version, bool IsSigned, SignatureStatus SignatureStatus, string SignatureDetail, bool IsMicrosoft, bool IsCritical, bool IsBroken)
    {
        public string? Sha256 { get; init; }
    }

    private sealed record CachedInspection(long Length, long LastWriteUtcTicks, FileInspection Result);
}
