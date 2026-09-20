using BootLens.Core.Domain;
using BootLens.Core.Services;
using Microsoft.Data.Sqlite;

namespace BootLens.Data;

public sealed class SqliteBootLensRepository : IBootLensRepository
{
    private readonly string _connectionString;

    public SqliteBootLensRepository(string? databasePath = null)
    {
        var path = databasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BootLens", "bootlens.db");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Shared }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER NOT NULL PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS startup_entries (id TEXT NOT NULL PRIMARY KEY, display_name TEXT NOT NULL, mechanism INTEGER NOT NULL, state INTEGER NOT NULL, publisher TEXT, description TEXT, executable_path TEXT, command_line TEXT, identifier TEXT, version TEXT, sha256 TEXT, is_signed INTEGER NOT NULL, is_microsoft INTEGER NOT NULL, is_critical INTEGER NOT NULL, is_broken INTEGER NOT NULL, first_seen_utc TEXT NOT NULL, last_seen_utc TEXT NOT NULL, cpu_ms REAL, disk_io_bytes REAL, peak_memory_bytes REAL, trigger TEXT, source_location TEXT);
            CREATE TABLE IF NOT EXISTS snapshots (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, created_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS snapshot_entries (snapshot_id INTEGER NOT NULL, entry_id TEXT NOT NULL, display_name TEXT NOT NULL, mechanism INTEGER NOT NULL, state INTEGER NOT NULL, publisher TEXT, executable_path TEXT, command_line TEXT, is_signed INTEGER NOT NULL, is_microsoft INTEGER NOT NULL, is_critical INTEGER NOT NULL, is_broken INTEGER NOT NULL, PRIMARY KEY(snapshot_id, entry_id));
            CREATE TABLE IF NOT EXISTS startup_changes (id INTEGER PRIMARY KEY AUTOINCREMENT, entry_id TEXT NOT NULL, change_type TEXT NOT NULL, summary TEXT NOT NULL, detected_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS undo_records (id INTEGER PRIMARY KEY AUTOINCREMENT, entry_id TEXT NOT NULL, action TEXT NOT NULL, original_state TEXT NOT NULL, created_utc TEXT NOT NULL, is_reverted INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS settings (key TEXT NOT NULL PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS boot_measurements (id INTEGER PRIMARY KEY AUTOINCREMENT, started_utc TEXT NOT NULL, duration_seconds REAL NOT NULL, source TEXT, configuration_fingerprint TEXT NOT NULL);
            INSERT OR IGNORE INTO schema_migrations(version) VALUES (1);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveEntriesAsync(IReadOnlyCollection<StartupEntry> entries, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM startup_entries";
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var entry in entries)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO startup_entries VALUES ($id,$name,$mechanism,$state,$publisher,$description,$path,$command,$identifier,$version,$sha,$signed,$microsoft,$critical,$broken,$first,$last,$cpu,$io,$memory,$trigger,$source)";
            AddEntryParameters(command, entry);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StartupEntry>> GetLatestEntriesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM startup_entries ORDER BY display_name";
        var result = new List<StartupEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadEntry(reader));
        return result;
    }

    public async Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var snapshotCommand = connection.CreateCommand();
        snapshotCommand.Transaction = transaction;
        snapshotCommand.CommandText = "INSERT INTO snapshots(name,created_utc) VALUES($name,$created); SELECT last_insert_rowid();";
        snapshotCommand.Parameters.AddWithValue("$name", snapshot.Name);
        snapshotCommand.Parameters.AddWithValue("$created", snapshot.CreatedUtc.ToString("O"));
        var id = Convert.ToInt64(await snapshotCommand.ExecuteScalarAsync(cancellationToken));
        foreach (var entry in snapshot.Entries)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO snapshot_entries(snapshot_id,entry_id,display_name,mechanism,state,publisher,executable_path,command_line,is_signed,is_microsoft,is_critical,is_broken) VALUES($snapshot,$id,$name,$mechanism,$state,$publisher,$path,$command,$signed,$microsoft,$critical,$broken)";
            command.Parameters.AddWithValue("$snapshot", id); command.Parameters.AddWithValue("$id", entry.Id); command.Parameters.AddWithValue("$name", entry.DisplayName); command.Parameters.AddWithValue("$mechanism", (int)entry.Mechanism); command.Parameters.AddWithValue("$state", (int)entry.State); command.Parameters.AddWithValue("$publisher", (object?)entry.Publisher ?? DBNull.Value); command.Parameters.AddWithValue("$path", (object?)entry.ExecutablePath ?? DBNull.Value); command.Parameters.AddWithValue("$command", (object?)entry.CommandLine ?? DBNull.Value); command.Parameters.AddWithValue("$signed", entry.IsSigned ? 1 : 0); command.Parameters.AddWithValue("$microsoft", entry.IsMicrosoft ? 1 : 0); command.Parameters.AddWithValue("$critical", entry.IsCritical ? 1 : 0); command.Parameters.AddWithValue("$broken", entry.IsBroken ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Snapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,created_utc FROM snapshots ORDER BY created_utc DESC";
        var snapshots = new List<Snapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) snapshots.Add(new Snapshot { Id = reader.GetInt64(0), Name = reader.GetString(1), CreatedUtc = DateTimeOffset.Parse(reader.GetString(2)), Entries = [] });
        return snapshots;
    }

    public async Task SaveChangesAsync(IReadOnlyCollection<StartupChange> changes, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        foreach (var change in changes)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO startup_changes(entry_id,change_type,summary,detected_utc) VALUES($id,$type,$summary,$detected)";
            command.Parameters.AddWithValue("$id", change.EntryId); command.Parameters.AddWithValue("$type", change.ChangeType); command.Parameters.AddWithValue("$summary", change.Summary); command.Parameters.AddWithValue("$detected", change.DetectedUtc.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<StartupChange>> GetRecentChangesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT entry_id,change_type,summary,detected_utc FROM startup_changes ORDER BY detected_utc DESC LIMIT 100";
        var result = new List<StartupChange>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new StartupChange { EntryId = reader.GetString(0), ChangeType = reader.GetString(1), Summary = reader.GetString(2), DetectedUtc = DateTimeOffset.Parse(reader.GetString(3)) });
        return result;
    }

    public async Task SaveUndoAsync(UndoRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO undo_records(entry_id,action,original_state,created_utc,is_reverted) VALUES($id,$action,$state,$created,$reverted)";
        command.Parameters.AddWithValue("$id", record.EntryId); command.Parameters.AddWithValue("$action", record.Action); command.Parameters.AddWithValue("$state", record.OriginalState); command.Parameters.AddWithValue("$created", record.CreatedUtc.ToString("O")); command.Parameters.AddWithValue("$reverted", record.IsReverted ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key=$key"; command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value"; command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveBootMeasurementAsync(BootMeasurement measurement, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO boot_measurements(started_utc,duration_seconds,source,configuration_fingerprint) VALUES($started,$duration,$source,$fingerprint)";
        command.Parameters.AddWithValue("$started", measurement.StartedUtc.ToString("O")); command.Parameters.AddWithValue("$duration", measurement.DurationSeconds); command.Parameters.AddWithValue("$source", (object?)measurement.Source ?? DBNull.Value); command.Parameters.AddWithValue("$fingerprint", measurement.ConfigurationFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BootMeasurement>> GetBootMeasurementsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,started_utc,duration_seconds,source,configuration_fingerprint FROM boot_measurements ORDER BY started_utc DESC LIMIT 100";
        var result = new List<BootMeasurement>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new BootMeasurement { Id = reader.GetInt64(0), StartedUtc = DateTimeOffset.Parse(reader.GetString(1)), DurationSeconds = reader.GetDouble(2), Source = reader.IsDBNull(3) ? null : reader.GetString(3), ConfigurationFingerprint = reader.GetString(4) });
        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddEntryParameters(SqliteCommand command, StartupEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id); command.Parameters.AddWithValue("$name", entry.DisplayName); command.Parameters.AddWithValue("$mechanism", (int)entry.Mechanism); command.Parameters.AddWithValue("$state", (int)entry.State); command.Parameters.AddWithValue("$publisher", (object?)entry.Publisher ?? DBNull.Value); command.Parameters.AddWithValue("$description", (object?)entry.Description ?? DBNull.Value); command.Parameters.AddWithValue("$path", (object?)entry.ExecutablePath ?? DBNull.Value); command.Parameters.AddWithValue("$command", (object?)entry.CommandLine ?? DBNull.Value); command.Parameters.AddWithValue("$identifier", (object?)entry.Identifier ?? DBNull.Value); command.Parameters.AddWithValue("$version", (object?)entry.Version ?? DBNull.Value); command.Parameters.AddWithValue("$sha", (object?)entry.Sha256 ?? DBNull.Value); command.Parameters.AddWithValue("$signed", entry.IsSigned ? 1 : 0); command.Parameters.AddWithValue("$microsoft", entry.IsMicrosoft ? 1 : 0); command.Parameters.AddWithValue("$critical", entry.IsCritical ? 1 : 0); command.Parameters.AddWithValue("$broken", entry.IsBroken ? 1 : 0); command.Parameters.AddWithValue("$first", entry.FirstSeenUtc.ToString("O")); command.Parameters.AddWithValue("$last", entry.LastSeenUtc.ToString("O")); command.Parameters.AddWithValue("$cpu", (object?)entry.CpuMilliseconds ?? DBNull.Value); command.Parameters.AddWithValue("$io", (object?)entry.DiskIoBytes ?? DBNull.Value); command.Parameters.AddWithValue("$memory", (object?)entry.PeakMemoryBytes ?? DBNull.Value); command.Parameters.AddWithValue("$trigger", (object?)entry.Trigger ?? DBNull.Value); command.Parameters.AddWithValue("$source", (object?)entry.SourceLocation ?? DBNull.Value);
    }

    private static StartupEntry ReadEntry(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0), DisplayName = reader.GetString(1), Mechanism = (StartupMechanism)reader.GetInt32(2), State = (StartupState)reader.GetInt32(3), Publisher = ReadString(reader, 4), Description = ReadString(reader, 5), ExecutablePath = ReadString(reader, 6), CommandLine = ReadString(reader, 7), Identifier = ReadString(reader, 8), Version = ReadString(reader, 9), Sha256 = ReadString(reader, 10), IsSigned = reader.GetInt32(11) == 1, IsMicrosoft = reader.GetInt32(12) == 1, IsCritical = reader.GetInt32(13) == 1, IsBroken = reader.GetInt32(14) == 1, FirstSeenUtc = DateTimeOffset.Parse(reader.GetString(15)), LastSeenUtc = DateTimeOffset.Parse(reader.GetString(16)), CpuMilliseconds = ReadDouble(reader, 17), DiskIoBytes = ReadDouble(reader, 18), PeakMemoryBytes = ReadDouble(reader, 19), Trigger = ReadString(reader, 20), SourceLocation = ReadString(reader, 21)
    };

    private static string? ReadString(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static double? ReadDouble(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetDouble(index);
}
