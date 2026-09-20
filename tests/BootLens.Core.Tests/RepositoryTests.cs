using BootLens.Core.Domain;
using BootLens.Data;
using Microsoft.Data.Sqlite;

namespace BootLens.Core.Tests;

public sealed class RepositoryTests
{
    [Fact]
    public async Task Repository_migrates_saves_and_reads_core_records()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bootlens-{Guid.NewGuid():N}.db");
        try
        {
            var repository = new SqliteBootLensRepository(path);
            await repository.InitializeAsync();
            var entry = new StartupEntry { Id = "test", DisplayName = "Test", Mechanism = StartupMechanism.RegistryRun, State = StartupState.Enabled, CommandLine = "test.exe" };
            await repository.SaveEntriesAsync([entry]);
            await repository.SetSettingAsync("language", "it");
            await repository.SaveSnapshotAsync(new Snapshot { Name = "Before", CreatedUtc = DateTimeOffset.UtcNow, Entries = [entry] });
            await repository.SaveChangesAsync([new StartupChange
            {
                EntryId = entry.Id,
                ChangeType = "Action",
                Summary = "Elemento disabilitato e verificato.",
                DetectedUtc = DateTimeOffset.UtcNow,
                DisplayName = entry.DisplayName,
                Mechanism = entry.Mechanism,
                PreviousState = StartupState.Enabled,
                CurrentState = StartupState.Disabled,
                PreviousCommand = entry.CommandLine,
                CurrentCommand = entry.CommandLine,
                SourceLocation = "Registry64:HKEY_CURRENT_USER\\Software\\BootLens",
                Verified = true,
                ResultMessage = "Elemento disabilitato e verificato."
            }]);
            await repository.SaveBootMeasurementAsync(new BootMeasurement { StartedUtc = DateTimeOffset.UtcNow, DurationSeconds = 12.5, ConfigurationFingerprint = "fingerprint" });
            Assert.Single(await repository.GetLatestEntriesAsync());
            Assert.Equal("it", await repository.GetSettingAsync("language"));
            var snapshots = await repository.GetSnapshotsAsync();
            Assert.Single(snapshots);
            Assert.Single(snapshots[0].Entries);
            Assert.Equal("test", snapshots[0].Entries[0].Id);
            var changes = await repository.GetRecentChangesAsync();
            Assert.Single(changes);
            Assert.Equal("Test", changes[0].DisplayName);
            Assert.Equal(StartupState.Enabled, changes[0].PreviousState);
            Assert.Equal(StartupState.Disabled, changes[0].CurrentState);
            Assert.True(changes[0].Verified);
            Assert.Equal("Elemento disabilitato e verificato.", changes[0].ResultMessage);
            Assert.Single(await repository.GetBootMeasurementsAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
            File.Delete(path + "-shm");
            File.Delete(path + "-wal");
        }
    }
}
