using BootLens.Core.Domain;

namespace BootLens.Core.Analysis;

public sealed class ChangeDetection
{
    public IReadOnlyList<StartupChange> Compare(IReadOnlyCollection<StartupEntry> previous, IReadOnlyCollection<StartupEntry> current, DateTimeOffset? detectedUtc = null)
    {
        var timestamp = detectedUtc ?? DateTimeOffset.UtcNow;
        var oldById = previous.ToDictionary(entry => entry.Id, StringComparer.OrdinalIgnoreCase);
        var newById = current.ToDictionary(entry => entry.Id, StringComparer.OrdinalIgnoreCase);
        var changes = new List<StartupChange>();
        foreach (var entry in current.Where(entry => !oldById.ContainsKey(entry.Id)))
            changes.Add(new StartupChange { EntryId = entry.Id, ChangeType = "Added", Summary = $"{entry.DisplayName} added", DetectedUtc = timestamp, DisplayName = entry.DisplayName, Mechanism = entry.Mechanism, CurrentState = entry.State, CurrentPath = entry.ExecutablePath, CurrentCommand = entry.CommandLine, Publisher = entry.Publisher, SourceLocation = entry.SourceLocation });
        foreach (var entry in previous.Where(entry => !newById.ContainsKey(entry.Id)))
            changes.Add(new StartupChange { EntryId = entry.Id, ChangeType = "Removed", Summary = $"{entry.DisplayName} removed", DetectedUtc = timestamp, DisplayName = entry.DisplayName, Mechanism = entry.Mechanism, PreviousState = entry.State, PreviousPath = entry.ExecutablePath, PreviousCommand = entry.CommandLine, Publisher = entry.Publisher, SourceLocation = entry.SourceLocation });
        foreach (var entry in current.Where(entry => oldById.TryGetValue(entry.Id, out _)))
        {
            var old = oldById[entry.Id];
            if (!string.Equals(old.CommandLine, entry.CommandLine, StringComparison.Ordinal) || !string.Equals(old.ExecutablePath, entry.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                changes.Add(new StartupChange { EntryId = entry.Id, ChangeType = "Modified", Summary = $"{entry.DisplayName} command or path changed", DetectedUtc = timestamp, DisplayName = entry.DisplayName, Mechanism = entry.Mechanism, PreviousState = old.State, CurrentState = entry.State, PreviousPath = old.ExecutablePath, CurrentPath = entry.ExecutablePath, PreviousCommand = old.CommandLine, CurrentCommand = entry.CommandLine, Publisher = entry.Publisher ?? old.Publisher, SourceLocation = entry.SourceLocation ?? old.SourceLocation });
            if (old.State != entry.State)
                changes.Add(new StartupChange { EntryId = entry.Id, ChangeType = "StateChanged", Summary = $"{entry.DisplayName} state changed", DetectedUtc = timestamp, DisplayName = entry.DisplayName, Mechanism = entry.Mechanism, PreviousState = old.State, CurrentState = entry.State, PreviousPath = old.ExecutablePath, CurrentPath = entry.ExecutablePath, PreviousCommand = old.CommandLine, CurrentCommand = entry.CommandLine, Publisher = entry.Publisher ?? old.Publisher, SourceLocation = entry.SourceLocation ?? old.SourceLocation });
        }
        return changes;
    }
}
