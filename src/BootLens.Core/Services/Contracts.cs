using BootLens.Core.Domain;

namespace BootLens.Core.Services;

public interface IStartupScanner
{
    Task<IReadOnlyList<StartupEntry>> ScanAsync(CancellationToken cancellationToken = default);
}

public interface IScanCoverageProvider
{
    IReadOnlyList<ScanSourceCoverage> LastCoverage { get; }
}

public interface IBootMeasurementProvider
{
    Task<BootMeasurement?> GetLatestAsync(IReadOnlyCollection<StartupEntry> entries, CancellationToken cancellationToken = default);
}

public interface IStartupModifier
{
    Task<OperationResult> DisableAsync(StartupEntry entry, CancellationToken cancellationToken = default);
    Task<OperationResult> EnableAsync(StartupEntry entry, CancellationToken cancellationToken = default);
}

public interface IBootLensRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveEntriesAsync(IReadOnlyCollection<StartupEntry> entries, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StartupEntry>> GetLatestEntriesAsync(CancellationToken cancellationToken = default);
    Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Snapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(IReadOnlyCollection<StartupChange> changes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StartupChange>> GetRecentChangesAsync(CancellationToken cancellationToken = default);
    Task SaveUndoAsync(UndoRecord record, CancellationToken cancellationToken = default);
    Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default);
    Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default);
    Task SaveBootMeasurementAsync(BootMeasurement measurement, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BootMeasurement>> GetBootMeasurementsAsync(CancellationToken cancellationToken = default);
}

public sealed record OperationResult(bool Succeeded, bool Verified, string Message, UndoRecord? UndoRecord = null)
{
    public static OperationResult Failure(string message) => new(false, false, message);
}
