using BootLens.Data;
using BootLens.Windows;

var repository = new SqliteBootLensRepository();
await repository.InitializeAsync();
var scanner = new WindowsStartupScanner();
var entries = await scanner.ScanAsync();
await repository.SaveEntriesAsync(entries);
Console.WriteLine($"BootLens agent captured {entries.Count} startup entries at {DateTimeOffset.Now:O}.");
