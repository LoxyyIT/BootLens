using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
var communityEnabled = false;
var databasePath = Environment.GetEnvironmentVariable("BOOTLENS_DB_PATH") ?? "/data/community.db";
var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
if (communityEnabled)
{
    var databaseDirectory = Path.GetDirectoryName(databasePath);
    if (!string.IsNullOrWhiteSpace(databaseDirectory)) Directory.CreateDirectory(databaseDirectory);
    await using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "CREATE TABLE IF NOT EXISTS observations (item_hash TEXT NOT NULL, mechanism TEXT NOT NULL, action TEXT NOT NULL, publisher_category TEXT, created_utc TEXT NOT NULL); CREATE INDEX IF NOT EXISTS ix_observations_item_hash ON observations(item_hash);";
    await command.ExecuteNonQueryAsync();
}

var app = builder.Build();
app.MapGet("/health", () =>
{
    if (!communityEnabled) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    return Results.Ok(new { status = "ok", service = "bootlens-community", version = "v1" });
});
app.MapPost("/api/v1/observations", async (ObservationRequest request) =>
{
    if (!communityEnabled) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    if (!request.IsValid()) return Results.BadRequest(new { error = "Invalid or incomplete observation." });
    await using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "INSERT INTO observations(item_hash,mechanism,action,publisher_category,created_utc) VALUES($hash,$mechanism,$action,$publisher,$created)";
    command.Parameters.AddWithValue("$hash", request.ItemHash); command.Parameters.AddWithValue("$mechanism", request.Mechanism); command.Parameters.AddWithValue("$action", request.Action); command.Parameters.AddWithValue("$publisher", (object?)request.PublisherCategory ?? DBNull.Value); command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
    await command.ExecuteNonQueryAsync();
    return Results.Accepted();
});
app.MapGet("/api/v1/observations/{itemHash}/summary", async (string itemHash) =>
{
    if (!communityEnabled) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    await using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT action,COUNT(*) FROM observations WHERE item_hash=$hash GROUP BY action"; command.Parameters.AddWithValue("$hash", itemHash);
    var rows = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) rows[reader.GetString(0)] = reader.GetInt64(1);
    return Results.Ok(new { itemHash, observations = rows.Values.Sum(), actions = rows });
});
app.Run();

public sealed record ObservationRequest(string ItemHash, string Mechanism, string Action, string? PublisherCategory)
{
    public bool IsValid() => ItemHash.Length is >= 16 and <= 128 && ItemHash.All(character => Uri.IsHexDigit(character)) && Mechanism.Length is > 0 and <= 64 && Action is "kept-enabled" or "disabled" or "delayed" or "restored";
}
