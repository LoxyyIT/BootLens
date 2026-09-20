using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using BootLens.Core.Domain;
using BootLens.Core.Services;

namespace BootLens.Windows;

public sealed class WindowsStartupModifier : IStartupModifier
{
    private const string BackupPath = "Software\\BootLens\\DisabledStartup";

    public Task<OperationResult> DisableAsync(StartupEntry entry, CancellationToken cancellationToken = default)
        => Task.Run(() => Change(entry, false, cancellationToken), cancellationToken);

    public Task<OperationResult> EnableAsync(StartupEntry entry, CancellationToken cancellationToken = default)
        => Task.Run(() => Change(entry, true, cancellationToken), cancellationToken);

    private static OperationResult Change(StartupEntry entry, bool enable, CancellationToken cancellationToken)
    {
        try
        {
            return entry.Mechanism switch
            {
                StartupMechanism.RegistryRun or StartupMechanism.RegistryRunOnce => ChangeRegistry(entry, enable),
                StartupMechanism.StartupFolder => ChangeStartupFolder(entry, enable),
                StartupMechanism.ScheduledTask => ChangeScheduledTask(entry, enable, cancellationToken),
                StartupMechanism.Service or StartupMechanism.Driver => ChangeService(entry, enable, cancellationToken),
                _ => OperationResult.Failure("Questo meccanismo di avvio non espone ancora un comando Windows reversibile sicuro.")
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return OperationResult.Failure($"Windows ha negato la modifica: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or SecurityException)
        {
            return OperationResult.Failure($"La modifica non è stata completata: {ex.Message}");
        }
    }

    private static OperationResult ChangeRegistry(StartupEntry entry, bool enable)
    {
        if (string.IsNullOrWhiteSpace(entry.Identifier) || string.IsNullOrWhiteSpace(entry.CommandLine)) return OperationResult.Failure("L'elemento non contiene abbastanza informazioni per modificarlo in sicurezza.");

        using var backupRoot = Registry.CurrentUser.CreateSubKey(BackupPath);
        var backupName = BackupKey(entry);
        if (enable)
        {
            using var stored = OpenBackup(backupRoot, entry);
            var command = stored?.GetValue("Command")?.ToString();
            var source = stored?.GetValue("Source")?.ToString();
            var identifier = stored?.GetValue("Identifier")?.ToString() ?? entry.Identifier;
            if (command is null || source is null) return OperationResult.Failure("Non è stato trovato il backup reversibile di BootLens per questo elemento.");
            using var sourceKey = OpenSourceKey(source, writable: true);
            if (sourceKey is null) return OperationResult.Failure("La posizione originale del Registro non è scrivibile.");
            sourceKey.SetValue(identifier, command);
            var restored = sourceKey.GetValue(identifier)?.ToString() == command;
            if (restored) DeleteBackup(backupRoot, backupName, entry.Identifier);
            return restored ? new OperationResult(true, true, "Elemento del Registro riabilitato.") : OperationResult.Failure("La verifica del Registro non è riuscita dopo il ripristino.");
        }

        using var sourceKeyToDisable = OpenSourceKey(entry.SourceLocation, writable: true);
        if (sourceKeyToDisable is null) return OperationResult.Failure("La posizione originale del Registro non è scrivibile.");
        using var backupEntry = backupRoot.CreateSubKey(backupName);
        backupEntry.SetValue("DisplayName", entry.DisplayName);
        backupEntry.SetValue("Command", entry.CommandLine);
        backupEntry.SetValue("Mechanism", entry.Mechanism.ToString());
        backupEntry.SetValue("Source", CanonicalSource(entry.SourceLocation));
        backupEntry.SetValue("Identifier", entry.Identifier);
        sourceKeyToDisable.DeleteValue(entry.Identifier, throwOnMissingValue: false);
        var disabled = sourceKeyToDisable.GetValue(entry.Identifier) is null && backupEntry.GetValue("Command")?.ToString() == entry.CommandLine;
        var undo = new UndoRecord { EntryId = entry.Id, Action = "Disable", OriginalState = entry.CommandLine, CreatedUtc = DateTimeOffset.UtcNow };
        return disabled ? new OperationResult(true, true, "Elemento del Registro disabilitato e salvato per il ripristino.", undo) : OperationResult.Failure("La verifica del Registro non è riuscita dopo la disabilitazione.");
    }

    private static OperationResult ChangeStartupFolder(StartupEntry entry, bool enable)
    {
        var originalPath = entry.Identifier ?? entry.ExecutablePath;
        if (string.IsNullOrWhiteSpace(originalPath)) return OperationResult.Failure("Il collegamento nella cartella Avvio non ha un percorso valido.");
        using var backupRoot = Registry.CurrentUser.CreateSubKey(BackupPath);
        var backupName = BackupKey(entry);
        if (enable)
        {
            using var stored = OpenBackup(backupRoot, entry);
            var disabledPath = stored?.GetValue("DisabledPath")?.ToString();
            var storedOriginal = stored?.GetValue("OriginalPath")?.ToString() ?? originalPath;
            if (string.IsNullOrWhiteSpace(disabledPath) || !File.Exists(disabledPath)) return OperationResult.Failure("Non è stato trovato il file disabilitato da ripristinare.");
            if (File.Exists(storedOriginal)) return OperationResult.Failure("Il file originale esiste già: il ripristino è stato fermato per evitare sovrascritture.");
            File.Move(disabledPath, storedOriginal);
            var restored = File.Exists(storedOriginal) && !File.Exists(disabledPath);
            if (restored) DeleteBackup(backupRoot, backupName, entry.Identifier);
            return restored ? new OperationResult(true, true, "Elemento della cartella Avvio riabilitato.") : OperationResult.Failure("La verifica del file non è riuscita dopo il ripristino.");
        }

        if (!File.Exists(originalPath)) return OperationResult.Failure("Il file dell'elemento di avvio non esiste più.");
        var disabledPathToCreate = originalPath + ".bootlens-disabled";
        if (File.Exists(disabledPathToCreate)) return OperationResult.Failure("Esiste già una copia disabilitata per questo elemento.");
        File.Move(originalPath, disabledPathToCreate);
        using var backupEntry = backupRoot.CreateSubKey(backupName);
        backupEntry.SetValue("DisplayName", entry.DisplayName);
        backupEntry.SetValue("Command", entry.CommandLine ?? originalPath);
        backupEntry.SetValue("Mechanism", entry.Mechanism.ToString());
        backupEntry.SetValue("Source", entry.SourceLocation ?? Path.GetDirectoryName(originalPath) ?? string.Empty);
        backupEntry.SetValue("Identifier", originalPath);
        backupEntry.SetValue("OriginalPath", originalPath);
        backupEntry.SetValue("DisabledPath", disabledPathToCreate);
        var disabled = !File.Exists(originalPath) && File.Exists(disabledPathToCreate);
        var undo = new UndoRecord { EntryId = entry.Id, Action = "Disable", OriginalState = originalPath, CreatedUtc = DateTimeOffset.UtcNow };
        return disabled ? new OperationResult(true, true, "Elemento della cartella Avvio disabilitato e salvato per il ripristino.", undo) : OperationResult.Failure("La verifica del file non è riuscita dopo la disabilitazione.");
    }

    private static OperationResult ChangeScheduledTask(StartupEntry entry, bool enable, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.Identifier)) return OperationResult.Failure("L'attività pianificata non ha un identificativo valido.");
        var result = RunTool("schtasks.exe", ["/Change", "/TN", entry.Identifier, enable ? "/ENABLE" : "/DISABLE"], cancellationToken);
        if (result.ExitCode != 0) return OperationResult.Failure($"Task Scheduler non ha applicato la modifica: {FirstLine(result.Error, result.Output)}");
        var state = RunTool("schtasks.exe", ["/Query", "/TN", entry.Identifier, "/FO", "LIST"], cancellationToken);
        var verified = state.ExitCode == 0 && (enable ? !ContainsDisabled(state.Output) : ContainsDisabled(state.Output));
        var message = enable ? "Attività pianificata riabilitata." : "Attività pianificata disabilitata.";
        var undo = new UndoRecord { EntryId = entry.Id, Action = enable ? "Enable" : "Disable", OriginalState = entry.State.ToString(), CreatedUtc = DateTimeOffset.UtcNow };
        return verified ? new OperationResult(true, true, message, undo) : OperationResult.Failure("La modifica dell'attività è stata eseguita ma non verificata.");
    }

    private static OperationResult ChangeService(StartupEntry entry, bool enable, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.Identifier)) return OperationResult.Failure("Il servizio non ha un identificativo valido.");
        using var backupRoot = Registry.CurrentUser.CreateSubKey(BackupPath);
        var backupName = BackupKey(entry);
        using var serviceKey = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{entry.Identifier}", writable: true);
        if (serviceKey is null) return OperationResult.Failure("La configurazione del servizio non è accessibile.");
        using var stored = OpenBackup(backupRoot, entry);
        var originalTrigger = stored?.GetValue("OriginalTrigger")?.ToString() ?? (entry.Trigger is "Disabled" or null ? "Auto" : entry.Trigger);
        var startValue = enable ? TriggerToScStart(originalTrigger) : "disabled";
        var result = RunTool("sc.exe", ["config", entry.Identifier, "start=", startValue], cancellationToken);
        if (result.ExitCode != 0) return OperationResult.Failure($"Service Control Manager non ha applicato la modifica: {FirstLine(result.Error, result.Output)}");
        var expected = enable ? TriggerToRegistryValue(originalTrigger) : 4;
        var verified = serviceKey.GetValue("Start") is int current && current == expected;
        if (verified && enable) DeleteBackup(backupRoot, backupName, entry.Identifier);
        if (!verified) return OperationResult.Failure("La modifica del servizio è stata eseguita ma non verificata.");
        var message = enable ? "Servizio riabilitato." : "Servizio disabilitato.";
        var undo = new UndoRecord { EntryId = entry.Id, Action = enable ? "Enable" : "Disable", OriginalState = entry.State.ToString(), CreatedUtc = DateTimeOffset.UtcNow };
        if (!enable)
        {
            using var backupEntry = backupRoot.CreateSubKey(backupName);
            backupEntry.SetValue("DisplayName", entry.DisplayName);
            backupEntry.SetValue("Mechanism", entry.Mechanism.ToString());
            backupEntry.SetValue("Identifier", entry.Identifier);
            backupEntry.SetValue("OriginalTrigger", originalTrigger);
        }
        return new OperationResult(true, true, message, undo);
    }

    private static (int ExitCode, string Output, string Error) RunTool(string fileName, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(fileName) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) return (-1, string.Empty, "Il comando Windows non è stato avviato.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);
        return (process.ExitCode, outputTask.Result, errorTask.Result);
    }

    private static RegistryKey? OpenSourceKey(string? source, bool writable)
    {
        var canonical = CanonicalSource(source);
        var view = source?.StartsWith("Registry32:", StringComparison.OrdinalIgnoreCase) == true ? RegistryView.Registry32 : RegistryView.Registry64;
        if (canonical.StartsWith("CurrentUser\\", StringComparison.OrdinalIgnoreCase)) return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view).OpenSubKey(canonical[12..], writable);
        if (canonical.StartsWith("LocalMachine\\", StringComparison.OrdinalIgnoreCase)) return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(canonical[13..], writable);
        return null;
    }

    private static string CanonicalSource(string? source)
    {
        var value = source?.Trim() ?? string.Empty;
        if (value.StartsWith("Registry32:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("Registry64:", StringComparison.OrdinalIgnoreCase)) value = value[(value.IndexOf(':') + 1)..];
        if (value.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase) || value.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)) return "CurrentUser\\" + value[(value.IndexOf('\\') + 1)..];
        if (value.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase) || value.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase)) return "LocalMachine\\" + value[(value.IndexOf('\\') + 1)..];
        return value;
    }

    private static RegistryKey? OpenBackup(RegistryKey? backupRoot, StartupEntry entry)
    {
        return backupRoot?.OpenSubKey(BackupKey(entry), writable: true) ?? backupRoot?.OpenSubKey(entry.Identifier ?? string.Empty, writable: true);
    }

    private static void DeleteBackup(RegistryKey? backupRoot, string backupName, string? legacyName)
    {
        backupRoot?.DeleteSubKeyTree(backupName, throwOnMissingSubKey: false);
        if (!string.IsNullOrWhiteSpace(legacyName)) backupRoot?.DeleteSubKeyTree(legacyName, throwOnMissingSubKey: false);
    }

    private static string BackupKey(StartupEntry entry)
    {
        var identity = $"{entry.Mechanism}|{entry.Identifier}|{entry.SourceLocation}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static string TriggerToScStart(string trigger) => trigger switch
    {
        "Manual" => "demand",
        "Delayed" => "delayed-auto",
        "Disabled" => "disabled",
        _ => "auto"
    };

    private static int TriggerToRegistryValue(string trigger) => trigger switch
    {
        "Manual" => 3,
        "Disabled" => 4,
        _ => 2
    };

    private static bool ContainsDisabled(string output) => output.Contains("Disabled", StringComparison.OrdinalIgnoreCase) || output.Contains("Disabilit", StringComparison.OrdinalIgnoreCase);

    private static string FirstLine(string error, string output) => (error + Environment.NewLine + output).Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "errore sconosciuto";
}
