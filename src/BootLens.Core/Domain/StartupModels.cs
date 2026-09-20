namespace BootLens.Core.Domain;

public enum StartupMechanism
{
    RegistryRun,
    RegistryRunOnce,
    StartupFolder,
    ScheduledTask,
    Service,
    Driver,
    Winlogon,
    Shell,
    Wmi,
    BootExecute,
    AppInit,
    KnownDll,
    Explorer,
    Codecs,
    ImageHijack,
    Winsock,
    PrintMonitor,
    LsaProvider,
    NetworkProvider,
    Office,
    PackagedApp,
    Unknown
}

public enum StartupState
{
    Enabled,
    Disabled,
    Broken,
    Protected,
    Unknown
}

public enum ScoreConfidence
{
    Low,
    Medium,
    High
}

public enum RecommendationKind
{
    KeepEnabled,
    UsuallyOptional,
    ConsiderDelaying,
    HighImpact,
    NeedsReview,
    ProtectedSystemComponent,
    InsufficientInformation
}

public sealed record StartupEntry
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required StartupMechanism Mechanism { get; init; }
    public required StartupState State { get; init; }
    public string? Publisher { get; init; }
    public string? Description { get; init; }
    public string? ExecutablePath { get; init; }
    public string? CommandLine { get; init; }
    public string? Identifier { get; init; }
    public string? Version { get; init; }
    public string? Sha256 { get; init; }
    public bool IsSigned { get; init; }
    public bool IsMicrosoft { get; init; }
    public bool IsCritical { get; init; }
    public bool IsBroken { get; init; }
    public DateTimeOffset FirstSeenUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenUtc { get; init; } = DateTimeOffset.UtcNow;
    public double? CpuMilliseconds { get; init; }
    public double? DiskIoBytes { get; init; }
    public double? PeakMemoryBytes { get; init; }
    public string? Trigger { get; init; }
    public string? SourceLocation { get; init; }
}

public sealed record BootMeasurement
{
    public long Id { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public double DurationSeconds { get; init; }
    public string? Source { get; init; }
    public string ConfigurationFingerprint { get; init; } = string.Empty;
}

public sealed record Snapshot
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public required IReadOnlyList<StartupEntry> Entries { get; init; }
}

public sealed record UndoRecord
{
    public long Id { get; init; }
    public required string EntryId { get; init; }
    public required string Action { get; init; }
    public required string OriginalState { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public bool IsReverted { get; init; }
}

public sealed record StartupChange
{
    public required string EntryId { get; init; }
    public required string ChangeType { get; init; }
    public required string Summary { get; init; }
    public DateTimeOffset DetectedUtc { get; init; }
    public string? DisplayName { get; init; }
    public StartupMechanism? Mechanism { get; init; }
    public StartupState? PreviousState { get; init; }
    public StartupState? CurrentState { get; init; }
    public string? PreviousPath { get; init; }
    public string? CurrentPath { get; init; }
    public string? PreviousCommand { get; init; }
    public string? CurrentCommand { get; init; }
    public string? Publisher { get; init; }
    public string? SourceLocation { get; init; }
    public bool Verified { get; init; }
    public string? ResultMessage { get; init; }
}
