using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows.Media;
using BootLens.Core.Analysis;
using BootLens.Core.Domain;
using BootLens.Core.Localization;
using BootLens.Core.Services;
using BootLens.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BootLens.App;

public partial class MainViewModel : ObservableObject
{
    private const string DisclaimerAcceptedKey = "safety.disclaimer.accepted";
    private const string DisclaimerVersionKey = "safety.disclaimer.version";
    private const string ConfirmActionsKey = "confirm.actions";
    private const string AutoScanOnLaunchKey = "scan.auto_on_launch";
    private const string SnapshotBeforeChangesKey = "changes.snapshot_before";
    private const string KeepSystemItemsBottomKey = "display.system_items_bottom";
    private const string CheckUpdatesOnLaunchKey = "updates.check_on_launch";
    private const string MonitorStartupChangesKey = "monitor.startup_changes";
    private readonly IBootLensRepository _repository;
    private readonly IStartupScanner _scanner;
    private readonly IStartupModifier _modifier;
    private readonly ChangeDetection _changeDetection;
    private readonly StartupAnalysisService _analysis;
    private readonly LocalizationService _localization;
    private readonly ThemeManager _themeManager;
    private readonly IBootMeasurementProvider _bootMeasurementProvider;
    private readonly UpdateService _updateService = new();
    private readonly DispatcherTimer _monitorTimer;
    private IReadOnlyList<AutorunsItem> _autorunsItems = [];
    private bool _loadingSettings;

    public MainViewModel(IBootLensRepository repository, IStartupScanner scanner, IStartupModifier modifier, IBootMeasurementProvider bootMeasurementProvider, ChangeDetection changeDetection, StartupAnalysisService analysis, LocalizationService localization, ThemeManager themeManager)
    {
        _repository = repository;
        _scanner = scanner;
        _modifier = modifier;
        _changeDetection = changeDetection;
        _analysis = analysis;
        _localization = localization;
        _themeManager = themeManager;
        _bootMeasurementProvider = bootMeasurementProvider;
        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _monitorTimer.Tick += async (_, _) =>
        {
            if (MonitorStartupChangesEnabled && !IsBusy) await ScanCoreAsync(true);
        };
        _localization.LanguageChanged += (_, _) =>
        {
            RebuildRows();
            OnPropertyChanged(nameof(Texts));
            OnPropertyChanged(nameof(CurrentPageTitle));
            OnPropertyChanged(nameof(Tagline));
            OnPropertyChanged(nameof(LightThemeLabel));
            OnPropertyChanged(nameof(LatestBootHint));
            foreach (var change in RecentChanges) change.RefreshLocalization();
            NotifySelectedEntry();
            OnPropertyChanged(nameof(VisibleEntries));
            RefreshCoverageRows();
            if (_autorunsItems.Count > 0) BuildAutorunsComparison(_autorunsItems);
        };
    }

    public IReadOnlyDictionary<string, string> Texts => new TextDictionary(_localization);
    public ObservableCollection<StartupEntry> Entries { get; } = [];
    public ObservableCollection<StartupEntryRow> EntryRows { get; } = [];
    public ObservableCollection<BootMeasurement> BootMeasurements { get; } = [];
    public ObservableCollection<ChangeLogRow> RecentChanges { get; } = [];
    public ObservableCollection<Snapshot> Snapshots { get; } = [];
    public ObservableCollection<CoverageViewRow> CoverageRows { get; } = [];
    public ObservableCollection<AutorunsComparisonViewRow> AutorunsComparison { get; } = [];
    public IEnumerable<StartupEntryRow> VisibleEntries
    {
        get
        {
            var rows = EntryRows.Where(MatchesSearch).Where(MatchesFilter);
        if (SortMode == "Name" && ActiveFilter == "All") return KeepSystemItemsBottomEnabled ? rows.OrderBy(row => row.IsSystemItem ? 1 : 0).ThenBy(row => row.DisplayName) : rows.OrderBy(row => row.DisplayName);
            return SortMode switch
            {
                "Score" => rows.OrderByDescending(row => row.ScoreValue).ThenBy(row => row.DisplayName),
                "Publisher" => rows.OrderBy(row => row.Publisher).ThenBy(row => row.DisplayName),
                "State" => rows.OrderBy(row => row.State).ThenBy(row => row.DisplayName),
                "Mechanism" => rows.OrderBy(row => row.Mechanism).ThenBy(row => row.DisplayName),
                "Process" => rows.OrderBy(row => row.ProcessName).ThenBy(row => row.DisplayName),
                "Path" => rows.OrderBy(row => row.ExecutablePath).ThenBy(row => row.DisplayName),
                "Impact" => rows.OrderBy(row => row.ScoreValue).ThenBy(row => row.DisplayName),
                _ => rows.OrderBy(row => row.DisplayName)
            };
        }
    }
    public IReadOnlyList<string> Languages => ["en", "it", "es", "fr"];
    public IReadOnlyList<string> Themes => ["Light"];
    public IReadOnlyList<string> Pages => ["Overview", "Startup", "Timeline", "BootHistory", "Changes", "Snapshots", "Coverage", "Advanced", "Settings"];
    public bool IsAllFilter => ActiveFilter == "All";
    public bool IsActiveFilter => ActiveFilter == "Active";
    public bool IsDisabledFilter => ActiveFilter == "Disabled";
    public bool IsBrokenFilter => ActiveFilter == "Broken";
    public bool IsHighImpactFilter => ActiveFilter == "HighImpact";
    public bool IsUnsignedFilter => ActiveFilter == "Unsigned";
    public bool IsInvalidSignatureFilter => ActiveFilter == "InvalidSignature";
    public bool IsMicrosoftFilter => ActiveFilter == "Microsoft";
    public bool IsThirdPartyFilter => ActiveFilter == "ThirdParty";
    public bool IsRegistryFilter => ActiveFilter == "Registry";
    public bool IsServicesFilter => ActiveFilter == "Services";
    public bool IsTasksFilter => ActiveFilter == "ScheduledTasks";
    public bool IsDriversFilter => ActiveFilter == "Drivers";
    public bool IsWinlogonFilter => ActiveFilter == "Winlogon";
    public bool IsShellFilter => ActiveFilter == "Shell";
    public bool IsWmiFilter => ActiveFilter == "Wmi";
    public bool IsAdvancedSurfaceFilter => ActiveFilter == "AdvancedSurfaces";
    public bool IsStartupFolderFilter => ActiveFilter == "StartupFolder";
    public bool IsInitialized { get; private set; }
    public bool IsOverviewPage => SelectedPage == "Overview";
    public bool IsStartupPage => SelectedPage == "Startup";
    public bool IsOverviewOrStartupPage => SelectedPage is "Overview" or "Startup";
    public bool IsUtilityPage => !IsOverviewOrStartupPage;
    public bool IsTimelinePage => SelectedPage == "Timeline";
    public bool IsBootHistoryPage => SelectedPage == "BootHistory";
    public bool IsChangesPage => SelectedPage == "Changes";
    public bool IsSnapshotsPage => SelectedPage == "Snapshots";
    public bool IsCommunityPage => SelectedPage == "Community";
    public bool IsAdvancedPage => SelectedPage == "Advanced";
    public bool IsSettingsPage => SelectedPage == "Settings";
    public bool IsCoveragePage => SelectedPage == "Coverage";
    public bool IsMissingFilter => ActiveFilter == "Missing";
    public bool IsDuplicateFilter => ActiveFilter == "Duplicates";
    private readonly HashSet<string> _duplicateEntryIds = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty] private StartupEntry? selectedEntry;
    [ObservableProperty] private StartupEntryRow? selectedRow;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string activeFilter = "All";
    [ObservableProperty] private string sortMode = "Name";
    [ObservableProperty] private string selectedPage = "Overview";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isFirstRun;
    [ObservableProperty] private bool safetyAccepted;
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string currentLanguage = "en";
    [ObservableProperty] private int activeItems;
    [ObservableProperty] private int disabledItems;
    [ObservableProperty] private int brokenItems;
    [ObservableProperty] private int unsignedItems;
    [ObservableProperty] private int unknownPublisherItems;
    [ObservableProperty] private int highImpactItems;
    [ObservableProperty] private int newItems;
    [ObservableProperty] private int modifiedItems;
    [ObservableProperty] private string healthScore = "—";
    [ObservableProperty] private string latestBoot = string.Empty;
    [ObservableProperty] private string medianBoot = string.Empty;
    [ObservableProperty] private string trend = string.Empty;
    [ObservableProperty] private bool isAdvancedMode;
    [ObservableProperty] private bool confirmActionsEnabled = true;
    [ObservableProperty] private bool autoScanOnLaunchEnabled;
    [ObservableProperty] private bool snapshotBeforeChangesEnabled = true;
    [ObservableProperty] private bool keepSystemItemsBottomEnabled = true;
    [ObservableProperty] private bool checkUpdatesOnLaunchEnabled = true;
    [ObservableProperty] private bool monitorStartupChangesEnabled;
    [ObservableProperty] private string autorunsImportName = string.Empty;
    [ObservableProperty] private string autorunsImportStatus = string.Empty;
    [ObservableProperty] private string updateStatus = string.Empty;
    [ObservableProperty] private bool updateAvailable;
    [ObservableProperty] private string updateUrl = UpdateService.ReleasesUrl;

    public string SelectedEntryTitle => SelectedEntry?.DisplayName ?? Texts["NoSelection"];
    public string SelectedEntryPublisher => SelectedEntry?.Publisher ?? Texts["UnknownPublisher"];
    public string SelectedEntryProcess => SelectedEntry is null ? Texts["NotAvailable"] : ProcessName(SelectedEntry);
    public string SelectedEntryPath => SelectedEntry is null ? Texts["NotAvailable"] : SelectedEntry.ExecutablePath ?? SelectedEntry.CommandLine ?? SelectedEntry.Identifier ?? ProcessName(SelectedEntry);
    public string SelectedEntryArguments => SelectedEntry is null ? Texts["NotAvailable"] : ExtractArguments(SelectedEntry);
    public string SelectedEntryFileStatus => SelectedEntry is null || !IsResolvedFilePath(SelectedEntry.ExecutablePath) ? Texts["NotAvailable"] : File.Exists(SelectedEntry.ExecutablePath) ? Texts["FilePresent"] : Texts["FileMissing"];
    public string SelectedEntryMechanism => SelectedEntry is null ? string.Empty : _localization.Get(MechanismKey(SelectedEntry.Mechanism));
    public string SelectedEntryState => SelectedEntry is null ? string.Empty : _localization.Get(StateKey(SelectedEntry.State));
    public bool SelectedEntryIsChecked => SelectedEntry?.State is StartupState.Enabled or StartupState.Protected;
    public string SelectedEntryDescription => SelectedEntry?.Description ?? Texts["NoDescription"];
    public string SelectedEntryCommand => SelectedEntry?.CommandLine ?? Texts["NotAvailable"];
    public string SelectedEntryVersion => SelectedEntry?.Version ?? Texts["NotAvailable"];
    public string SelectedEntryHash => SelectedEntry?.Sha256 ?? Texts["NotAvailable"];
    public string SelectedEntrySignature => SelectedEntry is null ? Texts["NotAvailable"] : Texts[SelectedEntry.SignatureStatus switch { SignatureStatus.Valid => "ValidSignature", SignatureStatus.Unsigned => "UnsignedSignature", SignatureStatus.Invalid => "InvalidSignature", SignatureStatus.Unavailable => "SignatureUnavailable", _ => SelectedEntry.IsSigned ? "ValidSignature" : "SignatureNotChecked" }];
    public string SelectedEntrySignatureDetail => SelectedEntry?.SignatureDetail ?? Texts["NotAvailable"];
    public string SelectedEntryTrust => SelectedEntry is null ? Texts["NotAvailable"] : string.Join(" · ", _analysis.Analyze(SelectedEntry, null, Entries).Trust.SignalKeys.Select(key => Texts[key])) is { Length: > 0 } trust ? trust : Texts["NoTrustSignals"];
    public string SelectedEntryRecommendation => SelectedEntry is null ? string.Empty : Texts[_analysis.Analyze(SelectedEntry, null, Entries).Recommendation.TitleKey];
    public string SelectedEntryRecommendationExplanation => SelectedEntry is null ? string.Empty : Texts[_analysis.Analyze(SelectedEntry, null, Entries).Recommendation.ExplanationKey];
    public string SelectedEntryCpu => SelectedEntry?.CpuMilliseconds is double cpu ? $"{cpu:0} ms" : Texts["NotMeasured"];
    public string SelectedEntryIo => SelectedEntry?.DiskIoBytes is double io ? FormatBytes(io) : Texts["NotMeasured"];
    public string SelectedEntryMemory => SelectedEntry?.PeakMemoryBytes is double memory ? FormatBytes(memory) : Texts["NotMeasured"];
    public string SelectedEntryFirstSeen => SelectedEntry is null ? Texts["NotAvailable"] : SelectedEntry.FirstSeenUtc.ToLocalTime().ToString("g");
    public string SelectedEntryLastSeen => SelectedEntry is null ? Texts["NotAvailable"] : SelectedEntry.LastSeenUtc.ToLocalTime().ToString("g");
    public string SelectedEntryTrigger => SelectedEntry?.Trigger ?? Texts["NotAvailable"];
    public string SelectedEntryIdentifier => SelectedEntry?.Identifier ?? Texts["NotAvailable"];
    public string SelectedEntryScore => SelectedEntry is null ? "—" : FormatScore(_analysis.Analyze(SelectedEntry, null, Entries).Efficiency, Texts);
    public string LatestBootHint => BootMeasurements.Count == 0 ? Texts["NotMeasured"] : Texts["LatestMeasurement"];
    public string WindowsVersionText => $"{Environment.OSVersion.VersionString} · {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}";
    public bool CanChangeSelected => !IsBusy && SelectedEntry is not null && CanChange(SelectedEntry);
    public string SelectedActionText => SelectedEntry is null ? string.Empty : !CanChange(SelectedEntry) ? Texts["ProtectedSystemComponent"] : SelectedEntry.State == StartupState.Disabled ? Texts["Enable"] : Texts["Disable"];
    public string CurrentPageTitle => _localization.Get(SelectedPage);
    public string Tagline => _localization.CurrentLanguage switch { "it" => "Chiarezza sull’avvio", "es" => "Claridad del inicio", "fr" => "Clarté du démarrage", _ => "Startup clarity" };
    public string LightThemeLabel => _localization.CurrentLanguage switch { "it" => "Chiaro", "es" => "Claro", "fr" => "Clair", _ => "Light" };
    public string CurrentVersionText => $"v{UpdateService.CurrentVersion}";
    public string ProjectUrl => UpdateService.RepositoryUrl;
    public string DataFolderPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BootLens");

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        CurrentLanguage = await _repository.GetSettingAsync("language") ?? "en";
        _localization.SetLanguage(CurrentLanguage);
        _themeManager.Apply("Light");
        await _repository.SetSettingAsync("theme", "Light");
        _loadingSettings = true;
        var acceptedVersion = await _repository.GetSettingAsync(DisclaimerVersionKey);
        IsFirstRun = acceptedVersion != _localization.Get("DisclaimerVersion");
        ConfirmActionsEnabled = await _repository.GetSettingAsync(ConfirmActionsKey) != "false";
        AutoScanOnLaunchEnabled = await ReadBoolSettingAsync(AutoScanOnLaunchKey, false);
        MonitorStartupChangesEnabled = await ReadBoolSettingAsync(MonitorStartupChangesKey, false);
        SnapshotBeforeChangesEnabled = await ReadBoolSettingAsync(SnapshotBeforeChangesKey, true);
        KeepSystemItemsBottomEnabled = await ReadBoolSettingAsync(KeepSystemItemsBottomKey, true);
        CheckUpdatesOnLaunchEnabled = await ReadBoolSettingAsync(CheckUpdatesOnLaunchKey, true);
        _loadingSettings = false;
        var entries = await _repository.GetLatestEntriesAsync();
        ReplaceEntries(entries);
        await LoadSecondaryDataAsync();
        await UpdateSummaryAsync();
        RefreshCoverageRows();
        IsInitialized = true;
        StatusText = entries.Count == 0 ? Texts["NoEntriesHint"] : $"{entries.Count} {Texts["ActiveItems"].ToLowerInvariant()}";
        if (AutoScanOnLaunchEnabled) await ScanAsync();
        await CaptureBootMeasurementAsync(Entries.ToArray());
        if (MonitorStartupChangesEnabled) _monitorTimer.Start();
        if (CheckUpdatesOnLaunchEnabled) await CheckForUpdatesAsync();
        else UpdateStatus = Texts["UpdateChecksDisabled"];
    }

    [RelayCommand]
    private async Task AcceptSafetyAsync()
    {
        if (!SafetyAccepted) return;
        await _repository.SetSettingAsync(DisclaimerAcceptedKey, DateTimeOffset.UtcNow.ToString("O"));
        await _repository.SetSettingAsync(DisclaimerVersionKey, _localization.Get("DisclaimerVersion"));
        IsFirstRun = false;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        await ScanCoreAsync(false);
    }

    private async Task ScanCoreAsync(bool background)
    {
        if (IsBusy) return;
        IsBusy = true;
        if (!background) StatusText = Texts["Scanning"];
        try
        {
            var previous = Entries.ToArray();
            var current = await _scanner.ScanAsync();
            IReadOnlyList<StartupChange> changes = previous.Length == 0 ? [] : _changeDetection.Compare(previous, current);
            await _repository.SaveEntriesAsync(current);
            if (changes.Count > 0) await _repository.SaveChangesAsync(changes);
            ReplaceEntries(current);
            NewItems = changes.Count(change => change.ChangeType == "Added");
            ModifiedItems = changes.Count(change => change.ChangeType is "Modified" or "StateChanged");
            await LoadSecondaryDataAsync();
            await UpdateSummaryAsync();
            RefreshCoverageRows();
            StatusText = background && changes.Count > 0
                ? string.Format(Texts["MonitorChangesDetected"], changes.Count, DateTime.Now.ToString("t"))
                : $"{current.Count} {Texts["ActiveItems"].ToLowerInvariant()} · {DateTime.Now:t}";
        }
        catch (OperationCanceledException) { StatusText = Texts["Cancel"]; }
        catch (Exception exception) { StatusText = exception.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanChangeSelected))]
    private async Task ToggleSelectedAsync()
    {
        if (SelectedEntry is not null) await ToggleEntryAsync(SelectedEntry);
    }

    [RelayCommand]
    private async Task ToggleRowAsync(StartupEntryRow? row)
    {
        if (IsBusy || row is null || !row.CanChange) return;
        await ToggleEntryAsync(row.Entry);
    }

    private async Task ToggleEntryAsync(StartupEntry entry)
    {
        if (IsBusy) return;
        var actionKey = entry.State == StartupState.Disabled ? "Enable" : "Disable";
        var shouldConfirm = await _repository.GetSettingAsync(ConfirmActionsKey) != "false";
        if (shouldConfirm)
        {
            var windowsManaged = entry.IsMicrosoft || entry.IsCritical || entry.Mechanism is StartupMechanism.Service or StartupMechanism.Driver or StartupMechanism.ScheduledTask || entry.SourceLocation?.Contains("LocalMachine\\", StringComparison.OrdinalIgnoreCase) == true;
            var dialog = new ActionConfirmationWindow(
                Texts["ConfirmActionTitle"],
                string.Format(Texts["ConfirmActionBody"], Texts[actionKey].ToLowerInvariant(), entry.DisplayName),
                Texts[windowsManaged ? "ConfirmWindowsActionWarning" : "ConfirmActionWarning"],
                Texts["CurrentState"],
                entry.State == StartupState.Disabled ? Texts["DisabledState"] : Texts["EnabledState"],
                Texts["NewState"],
                entry.State == StartupState.Disabled ? Texts["EnabledState"] : Texts["DisabledState"],
                Texts["DontAskAgain"],
                Texts["Confirm"],
                Texts["Cancel"])
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true) return;
            if (dialog.DontAskAgain) ConfirmActionsEnabled = false;
        }
        if (SnapshotBeforeChangesEnabled) await CreateSnapshotAsync();
        IsBusy = true;
        try
        {
            var enabling = entry.State == StartupState.Disabled;
            var result = enabling ? await _modifier.EnableAsync(entry) : await _modifier.DisableAsync(entry);
            if (result.Succeeded && result.Verified)
            {
                if (result.UndoRecord is not null) await _repository.SaveUndoAsync(result.UndoRecord);
                ApplyEntryState(entry, enabling ? StartupState.Enabled : StartupState.Disabled);
                await _repository.SaveEntriesAsync(Entries.ToArray());
                var change = CreateActionChange(entry, entry.State, enabling ? StartupState.Enabled : StartupState.Disabled, result);
                await _repository.SaveChangesAsync([change]);
                AddRecentChange(change);
                await UpdateSummaryAsync();
                StatusText = $"{result.Message} ✓";
            }
            else StatusText = result.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        UpdateStatus = Texts["CheckingUpdates"];
        var result = await _updateService.CheckAsync();
        UpdateAvailable = result.Succeeded && result.HasUpdate;
        UpdateUrl = result.ReleaseUrl;
        UpdateStatus = !result.Succeeded ? Texts["UpdateCheckUnavailable"] : result.HasUpdate ? string.Format(Texts["UpdateAvailable"], result.LatestVersion) : result.LatestVersion is null ? Texts["NoRelease"] : Texts["UpToDate"];
        OnPropertyChanged(nameof(UpdateStatus));
    }

    [RelayCommand]
    private async Task RestoreSnapshotAsync(Snapshot? snapshot)
    {
        if (IsBusy || snapshot is null || snapshot.Entries.Count == 0) return;
        var changes = snapshot.Entries
            .Select(target => (target, current: Entries.FirstOrDefault(entry => entry.Id == target.Id)))
            .Where(pair => pair.current is not null && pair.current.State != pair.target.State && CanChange(pair.current))
            .ToArray();
        if (changes.Length == 0)
        {
            StatusText = Texts["NoSnapshotChanges"];
            return;
        }

        var dialog = new ActionConfirmationWindow(
            Texts["RestoreSnapshot"],
            $"{snapshot.Name} · {changes.Length} {Texts["ActiveItems"].ToLowerInvariant()}",
            Texts["ConfirmActionWarning"],
            Texts["CurrentState"],
            Texts["Changed"],
            Texts["NewState"],
            Texts["RestoreSnapshot"],
            string.Empty,
            Texts["Confirm"],
            Texts["Cancel"])
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        var succeeded = 0;
        var actionChanges = new List<StartupChange>();
        try
        {
            foreach (var (target, current) in changes)
            {
                var result = target.State == StartupState.Disabled ? await _modifier.DisableAsync(current!) : await _modifier.EnableAsync(current!);
                if (!result.Succeeded || !result.Verified) continue;
                if (result.UndoRecord is not null) await _repository.SaveUndoAsync(result.UndoRecord);
                ApplyEntryState(current!, target.State);
                actionChanges.Add(CreateActionChange(current!, current!.State, target.State, result));
                succeeded++;
            }
            await _repository.SaveEntriesAsync(Entries.ToArray());
            if (actionChanges.Count > 0)
            {
                await _repository.SaveChangesAsync(actionChanges);
                foreach (var change in actionChanges) AddRecentChange(change);
            }
            await UpdateSummaryAsync();
            StatusText = succeeded == changes.Length
                ? $"{Texts["RestoreSnapshot"]} ✓"
                : $"{Texts["RestoreSnapshot"]}: {succeeded}/{changes.Length} · {Texts["NeedsReview"]}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        if (Entries.Count == 0)
        {
            StatusText = Texts["NoEntriesHint"];
            return;
        }

        try
        {
            Directory.CreateDirectory(DataFolderPath);
            var path = Path.Combine(DataFolderPath, $"bootlens-report-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            var report = new
            {
                ExportedUtc = DateTimeOffset.UtcNow,
                Version = UpdateService.CurrentVersion,
                HealthScore,
                Entries = Entries.Select(entry => new
                {
                    entry.Id,
                    entry.DisplayName,
                    entry.Mechanism,
                    entry.State,
                    entry.Publisher,
                    entry.ExecutablePath,
                    entry.CommandLine,
                    entry.SourceLocation,
                    entry.IsSigned,
                    entry.SignatureStatus,
                    entry.SignatureDetail,
                    entry.IsMicrosoft,
                    entry.IsCritical,
                    entry.IsBroken,
                    entry.CpuMilliseconds,
                    entry.DiskIoBytes,
                    entry.PeakMemoryBytes
                })
            };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            StatusText = string.Format(Texts["ReportExported"], Path.GetFileName(path));
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            StatusText = $"{Texts["ExportFailed"]}: {exception.Message}";
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(DataFolderPath);
            Process.Start(new ProcessStartInfo("explorer.exe", DataFolderPath) { UseShellExecute = true });
            StatusText = Texts["DataFolderOpened"];
        }
        catch (Exception exception)
        {
            StatusText = $"{Texts["OpenFolderFailed"]}: {exception.Message}";
        }
    }

    [RelayCommand]
    private void OpenProject()
    {
        OpenExternal(ProjectUrl);
    }

    [RelayCommand]
    private void OpenUpdate()
    {
        OpenExternal(UpdateUrl);
    }

    private void OpenExternal(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (InvalidOperationException)
        {
            StatusText = Texts["UpdateCheckUnavailable"];
        }
    }

    [RelayCommand]
    private void SetPage(string? page)
    {
        if (!string.IsNullOrWhiteSpace(page) && page != "Community") SelectedPage = page;
    }

    [RelayCommand]
    private void SetFilter(string? filter)
    {
        if (!string.IsNullOrWhiteSpace(filter)) ActiveFilter = filter;
    }

    [RelayCommand]
    private async Task ImportAutorunsAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Autoruns CSV (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            Title = Texts["AutorunsImport"]
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;
        try
        {
            var records = ParseCsv(await File.ReadAllTextAsync(dialog.FileName));
            if (records.Count < 2) throw new InvalidDataException(Texts["AutorunsCsvInvalid"]);
            var header = records[0].Select(value => value.Trim().TrimStart('\uFEFF')).ToArray();
            var categoryIndex = FindColumn(header, "Category", "Entry Location");
            var nameIndex = FindColumn(header, "Entry", "Autoruns Entry", "Name");
            var pathIndex = FindColumn(header, "Image Path", "Path");
            var commandIndex = FindColumn(header, "Launch String", "Command", "Image Path");
            var imported = records.Skip(1)
                .Where(row => row.Any(value => !string.IsNullOrWhiteSpace(value)))
                .Select(row => new AutorunsItem(
                    ReadColumn(row, categoryIndex),
                    ReadColumn(row, nameIndex),
                    ReadColumn(row, pathIndex),
                    ReadColumn(row, commandIndex)))
                .Where(item => !string.IsNullOrWhiteSpace(item.Category))
                .ToArray();
            if (imported.Length == 0) throw new InvalidDataException(Texts["AutorunsCsvInvalid"]);
            BuildAutorunsComparison(imported);
            _autorunsItems = imported;
            AutorunsImportName = Path.GetFileName(dialog.FileName);
            AutorunsImportStatus = string.Format(Texts["AutorunsImported"], imported.Length, AutorunsImportName);
            SelectedPage = "Coverage";
        }
        catch (Exception exception)
        {
            AutorunsImportStatus = $"{Texts["AutorunsImportFailed"]}: {exception.Message}";
        }
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void SetSort(string? sort)
    {
        if (!string.IsNullOrWhiteSpace(sort)) SortMode = sort;
    }

    [RelayCommand]
    private void SetMode(string? mode) => IsAdvancedMode = string.Equals(mode, "Advanced", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private async Task CreateSnapshotAsync()
    {
        if (Entries.Count == 0) return;
        var snapshot = new Snapshot { Name = $"BootLens {DateTime.Now:yyyy-MM-dd HH:mm}", CreatedUtc = DateTimeOffset.UtcNow, Entries = Entries.ToArray() };
        await _repository.SaveSnapshotAsync(snapshot);
        await LoadSecondaryDataAsync();
        StatusText = Texts["SnapshotCreated"];
    }

    [RelayCommand]
    private async Task SetLanguageAsync(string? language)
    {
        if (string.IsNullOrWhiteSpace(language) || !Languages.Contains(language)) return;
        CurrentLanguage = language;
        _localization.SetLanguage(language);
        await _repository.SetSettingAsync("language", language);
    }

    [RelayCommand]
    private async Task SetThemeAsync(string? theme)
    {
        if (!string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)) return;
        _themeManager.Apply("Light");
        await _repository.SetSettingAsync("theme", "Light");
    }

    partial void OnSelectedEntryChanged(StartupEntry? value)
    {
        NotifySelectedEntry();
        ToggleSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRowChanged(StartupEntryRow? value) => SelectedEntry = value?.Entry;

    partial void OnSelectedPageChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(IsOverviewPage));
        OnPropertyChanged(nameof(IsStartupPage));
        OnPropertyChanged(nameof(IsOverviewOrStartupPage));
        OnPropertyChanged(nameof(IsUtilityPage));
        OnPropertyChanged(nameof(IsTimelinePage));
        OnPropertyChanged(nameof(IsBootHistoryPage));
        OnPropertyChanged(nameof(IsChangesPage));
        OnPropertyChanged(nameof(IsSnapshotsPage));
        OnPropertyChanged(nameof(IsCommunityPage));
        OnPropertyChanged(nameof(IsAdvancedPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsCoveragePage));
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(VisibleEntries));
    partial void OnActiveFilterChanged(string value)
    {
        OnPropertyChanged(nameof(VisibleEntries));
        OnPropertyChanged(nameof(IsAllFilter));
        OnPropertyChanged(nameof(IsActiveFilter));
        OnPropertyChanged(nameof(IsDisabledFilter));
        OnPropertyChanged(nameof(IsBrokenFilter));
        OnPropertyChanged(nameof(IsHighImpactFilter));
        OnPropertyChanged(nameof(IsUnsignedFilter));
        OnPropertyChanged(nameof(IsInvalidSignatureFilter));
        OnPropertyChanged(nameof(IsMicrosoftFilter));
        OnPropertyChanged(nameof(IsThirdPartyFilter));
        OnPropertyChanged(nameof(IsRegistryFilter));
        OnPropertyChanged(nameof(IsServicesFilter));
        OnPropertyChanged(nameof(IsTasksFilter));
        OnPropertyChanged(nameof(IsDriversFilter));
        OnPropertyChanged(nameof(IsWinlogonFilter));
        OnPropertyChanged(nameof(IsShellFilter));
        OnPropertyChanged(nameof(IsWmiFilter));
        OnPropertyChanged(nameof(IsAdvancedSurfaceFilter));
        OnPropertyChanged(nameof(IsStartupFolderFilter));
        OnPropertyChanged(nameof(IsMissingFilter));
        OnPropertyChanged(nameof(IsDuplicateFilter));
    }
    partial void OnSortModeChanged(string value) => OnPropertyChanged(nameof(VisibleEntries));
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanChangeSelected));
        RebuildRows();
    }
    partial void OnConfirmActionsEnabledChanged(bool value) { if (!_loadingSettings) _ = _repository.SetSettingAsync(ConfirmActionsKey, value ? "true" : "false"); }
    partial void OnAutoScanOnLaunchEnabledChanged(bool value) { if (!_loadingSettings) _ = _repository.SetSettingAsync(AutoScanOnLaunchKey, value ? "true" : "false"); }
    partial void OnSnapshotBeforeChangesEnabledChanged(bool value) { if (!_loadingSettings) _ = _repository.SetSettingAsync(SnapshotBeforeChangesKey, value ? "true" : "false"); }
    partial void OnKeepSystemItemsBottomEnabledChanged(bool value) { if (!_loadingSettings) _ = _repository.SetSettingAsync(KeepSystemItemsBottomKey, value ? "true" : "false"); OnPropertyChanged(nameof(VisibleEntries)); }
    partial void OnCheckUpdatesOnLaunchEnabledChanged(bool value) { if (!_loadingSettings) _ = _repository.SetSettingAsync(CheckUpdatesOnLaunchKey, value ? "true" : "false"); }
    partial void OnMonitorStartupChangesEnabledChanged(bool value)
    {
        if (!_loadingSettings) _ = _repository.SetSettingAsync(MonitorStartupChangesKey, value ? "true" : "false");
        if (value && IsInitialized) _monitorTimer.Start();
        else _monitorTimer.Stop();
    }

    private void NotifySelectedEntry()
    {
        OnPropertyChanged(nameof(SelectedEntryTitle));
        OnPropertyChanged(nameof(SelectedEntryPublisher));
        OnPropertyChanged(nameof(SelectedEntryProcess));
        OnPropertyChanged(nameof(SelectedEntryPath));
        OnPropertyChanged(nameof(SelectedEntryArguments));
        OnPropertyChanged(nameof(SelectedEntryFileStatus));
        OnPropertyChanged(nameof(SelectedEntryMechanism));
        OnPropertyChanged(nameof(SelectedEntryState));
        OnPropertyChanged(nameof(SelectedEntryIsChecked));
        OnPropertyChanged(nameof(SelectedEntryDescription));
        OnPropertyChanged(nameof(SelectedEntryCommand));
        OnPropertyChanged(nameof(SelectedEntryVersion));
        OnPropertyChanged(nameof(SelectedEntryHash));
        OnPropertyChanged(nameof(SelectedEntrySignature));
        OnPropertyChanged(nameof(SelectedEntrySignatureDetail));
        OnPropertyChanged(nameof(SelectedEntryTrust));
        OnPropertyChanged(nameof(SelectedEntryRecommendation));
        OnPropertyChanged(nameof(SelectedEntryRecommendationExplanation));
        OnPropertyChanged(nameof(SelectedEntryCpu));
        OnPropertyChanged(nameof(SelectedEntryIo));
        OnPropertyChanged(nameof(SelectedEntryMemory));
        OnPropertyChanged(nameof(SelectedEntryFirstSeen));
        OnPropertyChanged(nameof(SelectedEntryLastSeen));
        OnPropertyChanged(nameof(SelectedEntryTrigger));
        OnPropertyChanged(nameof(SelectedEntryIdentifier));
        OnPropertyChanged(nameof(SelectedEntryScore));
        OnPropertyChanged(nameof(CanChangeSelected));
        OnPropertyChanged(nameof(SelectedActionText));
    }

    private async Task UpdateSummaryAsync()
    {
        ActiveItems = Entries.Count(entry => entry.State is StartupState.Enabled or StartupState.Protected);
        DisabledItems = Entries.Count(entry => entry.State == StartupState.Disabled);
        BrokenItems = Entries.Count(entry => entry.IsBroken || entry.State == StartupState.Broken);
        UnsignedItems = Entries.Count(entry => entry.SignatureStatus == SignatureStatus.Unsigned || entry.SignatureStatus == SignatureStatus.NotChecked && !entry.IsSigned);
        UnknownPublisherItems = Entries.Count(entry => string.IsNullOrWhiteSpace(entry.Publisher));
        HighImpactItems = Entries.Count(entry => _analysis.Analyze(entry).Efficiency.Value is > 0 and < 45);
        if (Entries.Count == 0) HealthScore = "—";
        else
        {
            var highImpactPenalty = HighImpactItems * 100.0 / Entries.Count * 0.3;
            var brokenPenalty = BrokenItems * 100.0 / Entries.Count * 0.45;
            var unknownPenalty = UnknownPublisherItems * 100.0 / Entries.Count * 0.15;
            var unsignedPenalty = UnsignedItems * 100.0 / Entries.Count * 0.1;
            HealthScore = Math.Clamp((int)Math.Round(100 - highImpactPenalty - brokenPenalty - unknownPenalty - unsignedPenalty), 1, 100).ToString();
        }
        var latest = BootMeasurements.FirstOrDefault();
        BootMeasurement[] sameConfiguration = latest is null ? [] : BootMeasurements.Where(item => item.ConfigurationFingerprint == latest.ConfigurationFingerprint).ToArray();
        LatestBoot = latest is null ? Texts["NotMeasured"] : $"{latest.DurationSeconds:0.0}s";
        MedianBoot = sameConfiguration.Length == 0 ? Texts["NotMeasured"] : $"{Median(sameConfiguration.Select(item => item.DurationSeconds)):0.0}s";
        var previousSameConfiguration = sameConfiguration.Skip(1).FirstOrDefault();
        Trend = latest is null || previousSameConfiguration is null || previousSameConfiguration.DurationSeconds <= 0
            ? Texts["NotMeasured"]
            : $"{((latest.DurationSeconds - previousSameConfiguration.DurationSeconds) / previousSameConfiguration.DurationSeconds):+0%;-0%;0%}";
        await Task.CompletedTask;
    }

    private async Task LoadSecondaryDataAsync()
    {
        BootMeasurements.Clear();
        foreach (var item in await _repository.GetBootMeasurementsAsync()) BootMeasurements.Add(item);
        RecentChanges.Clear();
        foreach (var item in await _repository.GetRecentChangesAsync()) RecentChanges.Add(new ChangeLogRow(item, _localization));
        Snapshots.Clear();
        foreach (var item in await _repository.GetSnapshotsAsync()) Snapshots.Add(item);
    }

    private async Task CaptureBootMeasurementAsync(IReadOnlyCollection<StartupEntry> entries)
    {
        var measurement = await _bootMeasurementProvider.GetLatestAsync(entries);
        if (measurement is null || BootMeasurements.Any(item => item.StartedUtc == measurement.StartedUtc && item.Source == measurement.Source)) return;
        await _repository.SaveBootMeasurementAsync(measurement);
        BootMeasurements.Insert(0, measurement);
        while (BootMeasurements.Count > 100) BootMeasurements.RemoveAt(BootMeasurements.Count - 1);
    }

    private void RefreshCoverageRows()
    {
        CoverageRows.Clear();
        var coverage = (_scanner as IScanCoverageProvider)?.LastCoverage ?? [];
        foreach (var source in coverage)
        {
            var category = Texts.TryGetValue($"Coverage{source.Key}", out var label) ? label : source.Key;
            var status = Texts.TryGetValue(source.StatusKey, out var statusLabel) ? statusLabel : source.StatusKey;
            var detail = source.Key == "Wmi" && source.Detail is { } detailText
                ? string.Format(Texts["WmiCoverageDetail"], detailText.Split(':')[0], detailText.Split(':').Last())
                : source.IsSupported ? Texts["CoverageSupported"] : Texts["CoverageUnsupported"];
            CoverageRows.Add(new CoverageViewRow(category, status, source.EntryCount, detail, source.IsSupported));
        }
        OnPropertyChanged(nameof(CoverageRows));
    }

    private void BuildAutorunsComparison(IReadOnlyCollection<AutorunsItem> autoruns)
    {
        AutorunsComparison.Clear();
        var bootLens = Entries.Select(entry => (Entry: entry, Category: AutorunsCategory(entry.Mechanism)))
            .Where(item => item.Category is not null)
            .GroupBy(item => item.Category!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => EntryIdentity(item.Entry)).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var autorunsByCategory = autoruns.GroupBy(item => NormalizeAutorunsCategory(item.Category), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key, group => group.Select(AutorunsIdentity).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var categories = bootLens.Keys.Concat(autorunsByCategory.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        foreach (var category in categories)
        {
            bootLens.TryGetValue(category, out var left);
            autorunsByCategory.TryGetValue(category, out var right);
            left ??= [];
            right ??= [];
            var shared = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
            var uiLabel = CategoryLabel(category);
            AutorunsComparison.Add(new AutorunsComparisonViewRow(uiLabel, left.Count, right.Count, shared, left.Count - shared, right.Count - shared));
        }
    }

    private void RebuildAutorunsComparisonIfPresent()
    {
        if (_autorunsItems.Count > 0) BuildAutorunsComparison(_autorunsItems);
    }

    private string CategoryLabel(string category) => Texts.TryGetValue($"Coverage{category.Replace(" ", string.Empty)}", out var label) ? label : category;

    private static string? AutorunsCategory(StartupMechanism mechanism) => mechanism switch
    {
        StartupMechanism.RegistryRun or StartupMechanism.RegistryRunOnce or StartupMechanism.StartupFolder or StartupMechanism.Wmi => "Logon",
        StartupMechanism.ScheduledTask => "Scheduled Tasks",
        StartupMechanism.Service => "Services",
        StartupMechanism.Driver => "Drivers",
        StartupMechanism.Winlogon => "Winlogon",
        StartupMechanism.Shell or StartupMechanism.Explorer => "Explorer",
        StartupMechanism.BootExecute => "Boot Execute",
        StartupMechanism.AppInit => "AppInit",
        StartupMechanism.KnownDll => "Known DLLs",
        StartupMechanism.Codecs => "Codecs",
        StartupMechanism.ImageHijack => "Image Hijacks",
        StartupMechanism.Winsock => "Winsock Providers",
        StartupMechanism.PrintMonitor => "Print Monitors",
        StartupMechanism.LsaProvider => "LSA Providers",
        StartupMechanism.NetworkProvider => "Network Providers",
        StartupMechanism.Office => "Office",
        StartupMechanism.PackagedApp => "Packaged Apps",
        _ => null
    };

    private static string NormalizeAutorunsCategory(string? category)
    {
        var key = category?.Trim().Replace('_', ' ') ?? string.Empty;
        if (key.Contains("scheduled task", StringComparison.OrdinalIgnoreCase) || key.Contains("task scheduler", StringComparison.OrdinalIgnoreCase)) return "Scheduled Tasks";
        if (key.Contains("logon", StringComparison.OrdinalIgnoreCase) || key.Contains("startup", StringComparison.OrdinalIgnoreCase)) return "Logon";
        if (key.Contains("service", StringComparison.OrdinalIgnoreCase)) return "Services";
        if (key.Contains("driver", StringComparison.OrdinalIgnoreCase)) return "Drivers";
        if (key.Contains("known dll", StringComparison.OrdinalIgnoreCase)) return "Known DLLs";
        if (key.Contains("boot execute", StringComparison.OrdinalIgnoreCase)) return "Boot Execute";
        if (key.Contains("image hijack", StringComparison.OrdinalIgnoreCase) || key.Contains("ifeo", StringComparison.OrdinalIgnoreCase)) return "Image Hijacks";
        if (key.Contains("winsock", StringComparison.OrdinalIgnoreCase)) return "Winsock Providers";
        if (key.Contains("print monitor", StringComparison.OrdinalIgnoreCase)) return "Print Monitors";
        if (key.Contains("lsa provider", StringComparison.OrdinalIgnoreCase)) return "LSA Providers";
        if (key.Contains("network provider", StringComparison.OrdinalIgnoreCase)) return "Network Providers";
        if (key.Contains("appinit", StringComparison.OrdinalIgnoreCase)) return "AppInit";
        if (key.Contains("codec", StringComparison.OrdinalIgnoreCase)) return "Codecs";
        if (key.Contains("explorer", StringComparison.OrdinalIgnoreCase)) return "Explorer";
        if (key.Contains("winlogon", StringComparison.OrdinalIgnoreCase)) return "Winlogon";
        if (key.Contains("office", StringComparison.OrdinalIgnoreCase)) return "Office";
        if (key.Contains("packaged", StringComparison.OrdinalIgnoreCase)) return "Packaged Apps";
        return key;
    }

    private static string EntryIdentity(StartupEntry entry) => NormalizeIdentity(entry.ExecutablePath ?? WindowsStartupScanner.ExtractExecutablePath(entry.CommandLine ?? string.Empty) ?? entry.CommandLine ?? string.Empty);
    private static string AutorunsIdentity(AutorunsItem item) => NormalizeIdentity(item.Path ?? WindowsStartupScanner.ExtractExecutablePath(item.Command ?? string.Empty) ?? item.Command ?? item.Name ?? string.Empty);
    private static string NormalizeIdentity(string value) => Environment.ExpandEnvironmentVariables(value.Trim().Trim('"').Replace('/', '\\')).TrimEnd('\\');

    private static int FindColumn(IReadOnlyList<string> headers, params string[] names)
    {
        for (var index = 0; index < headers.Count; index++)
            if (names.Any(name => string.Equals(headers[index].Trim(), name, StringComparison.OrdinalIgnoreCase))) return index;
        return -1;
    }

    private static string? ReadColumn(IReadOnlyList<string> columns, int index) => index >= 0 && index < columns.Count ? columns[index].Trim() : null;

    private static List<List<string>> ParseCsv(string input)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            if (character == '"')
            {
                if (quoted && index + 1 < input.Length && input[index + 1] == '"') { field.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { row.Add(field.ToString()); field.Clear(); }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < input.Length && input[index + 1] == '\n') index++;
                row.Add(field.ToString()); field.Clear();
                if (row.Any(value => !string.IsNullOrWhiteSpace(value))) rows.Add(row);
                row = [];
            }
            else field.Append(character);
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            if (row.Any(value => !string.IsNullOrWhiteSpace(value))) rows.Add(row);
        }
        return rows;
    }

    private sealed record AutorunsItem(string? Category, string? Name, string? Path, string? Command);
    public sealed record CoverageViewRow(string Category, string Status, int Count, string Detail, bool IsSupported);
    public sealed record AutorunsComparisonViewRow(string Category, int BootLensCount, int AutorunsCount, int SharedCount, int OnlyBootLensCount, int OnlyAutorunsCount);

    private void ReplaceEntries(IEnumerable<StartupEntry> entries)
    {
        Entries.Clear();
        foreach (var entry in entries) Entries.Add(entry);
        RebuildRows();
        RebuildAutorunsComparisonIfPresent();
        OnPropertyChanged(nameof(VisibleEntries));
    }

    private void AddRecentChange(StartupChange change)
    {
        RecentChanges.Insert(0, new ChangeLogRow(change, _localization));
        while (RecentChanges.Count > 100) RecentChanges.RemoveAt(RecentChanges.Count - 1);
    }

    private static StartupChange CreateActionChange(StartupEntry entry, StartupState previousState, StartupState currentState, OperationResult result) => new()
    {
        EntryId = entry.Id,
        ChangeType = "Action",
        Summary = result.Message,
        DetectedUtc = DateTimeOffset.UtcNow,
        DisplayName = entry.DisplayName,
        Mechanism = entry.Mechanism,
        PreviousState = previousState,
        CurrentState = currentState,
        PreviousPath = entry.ExecutablePath,
        CurrentPath = entry.ExecutablePath,
        PreviousCommand = entry.CommandLine,
        CurrentCommand = entry.CommandLine,
        Publisher = entry.Publisher,
        SourceLocation = entry.SourceLocation,
        Verified = result.Verified,
        ResultMessage = result.Message
    };

    private void RebuildRows()
    {
        var selectedId = SelectedEntry?.Id;
        _duplicateEntryIds.Clear();
        foreach (var group in Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.ExecutablePath))
                     .GroupBy(entry => NormalizeIdentity(entry.ExecutablePath!), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
            foreach (var entry in group) _duplicateEntryIds.Add(entry.Id);
        EntryRows.Clear();
        foreach (var entry in Entries) EntryRows.Add(new StartupEntryRow(entry, _localization, _analysis, Entries));
        SelectedRow = EntryRows.FirstOrDefault(row => row.Entry.Id == selectedId);
    }

    private bool MatchesSearch(StartupEntryRow row)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return new[] { row.Entry.DisplayName, row.ProcessName, row.Publisher, row.Mechanism, row.State, row.Entry.Description, row.Entry.ExecutablePath, row.Entry.CommandLine, row.Entry.SourceLocation, row.Entry.Trigger, row.Entry.Version, row.Entry.Identifier }
            .Any(value => value?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true);
    }

    private bool MatchesFilter(StartupEntryRow row) => ActiveFilter switch
    {
        "Active" => row.Entry.State is StartupState.Enabled or StartupState.Protected,
        "Disabled" => row.Entry.State == StartupState.Disabled,
        "Broken" => row.Entry.IsBroken || row.Entry.State == StartupState.Broken,
        "Missing" => row.Entry.IsBroken || IsResolvedFilePath(row.Entry.ExecutablePath) && !File.Exists(row.Entry.ExecutablePath),
        "Duplicates" => _duplicateEntryIds.Contains(row.Entry.Id),
        "HighImpact" => row.ScoreValue is > 0 and < 45,
        "Unsigned" => row.Entry.SignatureStatus == SignatureStatus.Unsigned || row.Entry.SignatureStatus == SignatureStatus.NotChecked && !row.Entry.IsSigned,
        "InvalidSignature" => row.Entry.SignatureStatus == SignatureStatus.Invalid,
        "Microsoft" => row.Entry.IsMicrosoft,
        "ThirdParty" => !row.Entry.IsMicrosoft,
        "Registry" => row.Entry.Mechanism is StartupMechanism.RegistryRun or StartupMechanism.RegistryRunOnce,
        "Services" => row.Entry.Mechanism == StartupMechanism.Service,
        "ScheduledTasks" => row.Entry.Mechanism == StartupMechanism.ScheduledTask,
        "StartupFolder" => row.Entry.Mechanism == StartupMechanism.StartupFolder,
        "Drivers" => row.Entry.Mechanism == StartupMechanism.Driver,
        "Winlogon" => row.Entry.Mechanism == StartupMechanism.Winlogon,
        "Shell" => row.Entry.Mechanism == StartupMechanism.Shell,
        "Wmi" => row.Entry.Mechanism == StartupMechanism.Wmi,
        "AdvancedSurfaces" => row.Entry.Mechanism is StartupMechanism.BootExecute or StartupMechanism.AppInit or StartupMechanism.KnownDll or StartupMechanism.Explorer or StartupMechanism.Codecs or StartupMechanism.ImageHijack or StartupMechanism.Winsock or StartupMechanism.PrintMonitor or StartupMechanism.LsaProvider or StartupMechanism.NetworkProvider or StartupMechanism.Office or StartupMechanism.PackagedApp,
        _ => true
    };

    private void ApplyEntryState(StartupEntry entry, StartupState state)
    {
        var index = Entries.IndexOf(entry);
        if (index < 0) return;
        var updated = entry with { State = state, IsBroken = false };
        Entries[index] = updated;
        if (SelectedEntry?.Id == entry.Id) SelectedEntry = updated;
        RebuildRows();
        OnPropertyChanged(nameof(VisibleEntries));
        NotifySelectedEntry();
    }

    private static bool CanChange(StartupEntry entry) => entry.Mechanism is StartupMechanism.RegistryRun or StartupMechanism.RegistryRunOnce or StartupMechanism.StartupFolder or StartupMechanism.ScheduledTask or StartupMechanism.Service or StartupMechanism.Driver
        && !entry.IsCritical
        && entry.State != StartupState.Protected
        && !string.IsNullOrWhiteSpace(entry.Identifier ?? entry.ExecutablePath ?? entry.CommandLine);

    private static string MechanismKey(StartupMechanism mechanism) => mechanism switch
    {
        StartupMechanism.RegistryRun or StartupMechanism.RegistryRunOnce => "Registry",
        StartupMechanism.StartupFolder => "Startup",
        StartupMechanism.ScheduledTask => "ScheduledTasks",
        StartupMechanism.Service => "Services",
        StartupMechanism.Driver => "Drivers",
        StartupMechanism.Winlogon => "Winlogon",
        StartupMechanism.Shell => "Shell",
        StartupMechanism.Wmi => "Wmi",
        StartupMechanism.BootExecute => "BootExecute",
        StartupMechanism.AppInit => "AppInit",
        StartupMechanism.KnownDll => "KnownDll",
        StartupMechanism.Explorer => "Explorer",
        StartupMechanism.Codecs => "Codecs",
        StartupMechanism.ImageHijack => "ImageHijack",
        StartupMechanism.Winsock => "Winsock",
        StartupMechanism.PrintMonitor => "PrintMonitor",
        StartupMechanism.LsaProvider => "LsaProvider",
        StartupMechanism.NetworkProvider => "NetworkProvider",
        StartupMechanism.Office => "Office",
        StartupMechanism.PackagedApp => "PackagedApp",
        _ => "Advanced"
    };

    private static string StateKey(StartupState state) => state switch
    {
        StartupState.Enabled => "EnabledState",
        StartupState.Disabled => "DisabledState",
        StartupState.Broken => "NeedsReview",
        StartupState.Protected => "Protection",
        _ => "InsufficientInformation"
    };

    private static string FormatScore(EfficiencyScore score, IReadOnlyDictionary<string, string> texts) => $"{score.Value}/100";

    private string ExtractArguments(StartupEntry entry)
    {
        var command = entry.CommandLine?.Trim();
        var path = entry.ExecutablePath;
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(path)) return Texts["NotAvailable"];
        int offset;
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            offset = end >= 0 ? end + 1 : path.Length;
        }
        else
        {
            var index = command.IndexOf(path, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) offset = index + path.Length;
            else
            {
                var executableEnd = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                offset = executableEnd >= 0 ? executableEnd + 4 : Math.Min(path.Length, command.Length);
            }
        }
        var arguments = command[Math.Min(offset, command.Length)..].Trim();
        return arguments.Length == 0 ? Texts["NotAvailable"] : arguments;
    }

    private static bool IsResolvedFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (Path.IsPathRooted(path)) return true;
        return path.Contains(".exe", StringComparison.OrdinalIgnoreCase)
            || path.Contains(".dll", StringComparison.OrdinalIgnoreCase)
            || path.Contains(".sys", StringComparison.OrdinalIgnoreCase)
            || path.Contains(".lnk", StringComparison.OrdinalIgnoreCase);
    }

    private static string ProcessName(StartupEntry entry)
    {
        var path = entry.ExecutablePath?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path)) return entry.DisplayName;
        var process = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(process) ? entry.DisplayName : process;
    }

    private static string FormatBytes(double value) => value >= 1024 * 1024 * 1024 ? $"{value / (1024 * 1024 * 1024):0.0} GB" : value >= 1024 * 1024 ? $"{value / (1024 * 1024):0.0} MB" : $"{value / 1024:0} KB";

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0) return 0;
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }

    private async Task<bool> ReadBoolSettingAsync(string key, bool fallback)
    {
        var value = await _repository.GetSettingAsync(key);
        return value is null ? fallback : string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class ChangeLogRow(StartupChange change, LocalizationService localization) : ObservableObject
    {
        private StartupChange Change { get; } = change;
        public string DisplayName => Change.DisplayName ?? Change.EntryId;
        public string TypeLabel => localization.Get(Change.ChangeType switch
        {
            "Added" => "ChangeAdded",
            "Removed" => "ChangeRemoved",
            "Modified" => "ChangeModified",
            "StateChanged" => "ChangeState",
            "Action" => "ChangeAction",
            _ => "ChangeOther"
        });
        public string Summary => Change.Summary;
        public string Timestamp => Change.DetectedUtc.ToLocalTime().ToString("g");
        public string Mechanism => $"{localization.Get("Mechanism")}: {(Change.Mechanism is { } mechanism ? localization.Get(MechanismKey(mechanism)) : localization.Get("NotAvailable"))}";
        public string Publisher => $"{localization.Get("Publisher")}: {ValueOrDash(Change.Publisher)}";
        public string Source => $"{localization.Get("SourceLabel")}: {ValueOrDash(Change.SourceLocation)}";
        public string BeforeState => $"{localization.Get("BeforeLabel")}: {FormatState(Change.PreviousState)}";
        public string AfterState => $"{localization.Get("AfterLabel")}: {FormatState(Change.CurrentState)}";
        public string Path => $"{localization.Get("PathLabel")}: {ValueOrDash(Change.CurrentPath ?? Change.PreviousPath)}";
        public string Command => $"{localization.Get("CommandLabel")}: {ValueOrDash(Change.CurrentCommand ?? Change.PreviousCommand)}";
        public string Result => $"{localization.Get("ResultLabel")}: {ValueOrDash(Change.ResultMessage ?? Change.Summary)}";
        public string Verification => Change.Verified ? localization.Get("VerifiedChange") : localization.Get("DetectedChange");

        public void RefreshLocalization() => OnPropertyChanged(string.Empty);

        private string FormatState(StartupState? state) => state is { } value ? localization.Get(StateKey(value)) : localization.Get("NotAvailable");
        private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    public sealed class StartupEntryRow(StartupEntry entry, LocalizationService localization, StartupAnalysisService analysis, IReadOnlyCollection<StartupEntry> allEntries)
    {
        public StartupEntry Entry { get; } = entry;
        public string DisplayName => Entry.DisplayName;
        public string ProcessName => MainViewModel.ProcessName(Entry);
        public string ExecutablePath => Entry.ExecutablePath ?? Entry.CommandLine ?? Entry.Identifier ?? ProcessName;
        public ImageSource? IconSource { get; } = StartupIconProvider.Get(entry.CommandLine ?? entry.ExecutablePath);
        public bool HasIcon => IconSource is not null;
        public string Publisher => Entry.Publisher ?? localization.Get("UnknownPublisher");
        public string Mechanism => localization.Get(MechanismKey(Entry.Mechanism));
        public string State => localization.Get(StateKey(Entry.State));
        public int ScoreValue => analysis.Analyze(Entry, null, allEntries.ToArray()).Efficiency.Value;
        public string Score => $"{ScoreValue}/100";
        public string Trust => Entry.IsMicrosoft ? localization.Get("Microsoft") : Entry.IsSigned ? localization.Get("PublisherVerified") : localization.Get("SignatureUnverified");
        public string ToggleText => !CanChange ? localization.Get("ProtectedSystemComponent") : Entry.State == StartupState.Disabled ? localization.Get("Enable") : localization.Get("Disable");
        public bool IsChecked => Entry.State is StartupState.Enabled or StartupState.Protected;
        public bool CanChange => MainViewModel.CanChange(Entry);
        public bool IsSystemItem => Entry.IsMicrosoft || Entry.IsCritical;
        public string IconGlyph => Entry.Mechanism switch
        {
            StartupMechanism.Service => "⚙",
            StartupMechanism.ScheduledTask => "◷",
            StartupMechanism.StartupFolder => "▣",
            StartupMechanism.Driver => "◆",
            StartupMechanism.Winlogon => "⌁",
            StartupMechanism.Shell => "⌂",
            StartupMechanism.Wmi => "◌",
            StartupMechanism.BootExecute => "▶",
            StartupMechanism.AppInit => "◎",
            StartupMechanism.KnownDll => "◇",
            StartupMechanism.Explorer => "◈",
            StartupMechanism.Codecs => "♫",
            StartupMechanism.ImageHijack => "⚠",
            StartupMechanism.Winsock => "⇄",
            StartupMechanism.PrintMonitor => "▤",
            StartupMechanism.LsaProvider => "♢",
            StartupMechanism.NetworkProvider => "⌘",
            StartupMechanism.Office => "▦",
            StartupMechanism.PackagedApp => "▥",
            _ when Entry.IsMicrosoft => "⊞",
            _ => "⌁"
        };
    }

    private sealed class TextDictionary(LocalizationService service) : IReadOnlyDictionary<string, string>
    {
        public string this[string key] => service.Get(key);
        public IEnumerable<string> Keys => [];
        public IEnumerable<string> Values => [];
        public int Count => 0;
        public bool ContainsKey(string key) => true;
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Enumerable.Empty<KeyValuePair<string, string>>().GetEnumerator();
        public bool TryGetValue(string key, out string value) { value = service.Get(key); return true; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
