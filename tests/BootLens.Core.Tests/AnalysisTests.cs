using BootLens.Core.Analysis;
using BootLens.Core.Domain;

namespace BootLens.Core.Tests;

public sealed class AnalysisTests
{
    [Fact]
    public void Score_without_observations_is_low_and_unmeasured()
    {
        var result = new StartupScoring().Calculate(new StartupMetrics(null, null, null, null, 0, false));
        Assert.Equal(0, result.Value);
        Assert.Equal(ScoreConfidence.Low, result.Confidence);
    }

    [Fact]
    public void Score_is_bounded_and_delaying_can_improve_efficiency()
    {
        var scoring = new StartupScoring();
        var normal = scoring.Calculate(new StartupMetrics(500, 10_000_000, 100_000_000, 500, 10, false));
        var delayed = scoring.Calculate(new StartupMetrics(500, 10_000_000, 100_000_000, 500, 10, true));
        Assert.InRange(normal.Value, 0, 100);
        Assert.True(delayed.Value >= normal.Value);
        Assert.Equal(ScoreConfidence.High, normal.Confidence);
    }

    [Fact]
    public void Change_detection_finds_added_removed_and_modified_entries()
    {
        var oldEntry = Entry("a", "old.exe");
        var current = new[] { oldEntry with { CommandLine = "new.exe" }, Entry("b", "b.exe") };
        var changes = new ChangeDetection().Compare([oldEntry, Entry("removed", "r.exe")], current);
        Assert.Contains(changes, change => change.ChangeType == "Modified");
        Assert.Contains(changes, change => change.ChangeType == "Added");
        Assert.Contains(changes, change => change.ChangeType == "Removed");
    }

    [Fact]
    public void Analysis_detects_duplicates_and_missing_files_without_claiming_malware()
    {
        var entry = Entry("a", Path.Combine(Path.GetTempPath(), "bootlens-no-such-file.exe"));
        var duplicate = entry with { Id = "b", DisplayName = "Second" };
        var result = new StartupAnalysisService(new StartupScoring()).Analyze(entry, allEntries: [entry, duplicate]);
        Assert.Contains("b", result.DuplicateEntryIds);
        Assert.Equal(RecommendationKind.NeedsReview, result.Recommendation.Kind);
        Assert.Contains("MissingExecutable", result.Trust.SignalKeys);
    }

    [Fact]
    public void Analysis_provides_a_visible_estimate_without_measurements()
    {
        var entry = Entry("estimated", Path.Combine(Path.GetTempPath(), "bootlens-estimated.exe"));
        var result = new StartupAnalysisService(new StartupScoring()).Analyze(entry);
        Assert.InRange(result.Efficiency.Value, 1, 100);
        Assert.Equal(ScoreConfidence.Low, result.Efficiency.Confidence);
        Assert.Equal("ScoreEstimated", result.Efficiency.ExplanationKey);
    }

    [Fact]
    public void Wilson_interval_reports_low_confidence_for_small_samples()
    {
        var result = new CommunityConfidence().Estimate(8, 10);
        Assert.Equal(10, result.Observations);
        Assert.Equal("Low", result.ConfidenceKey);
        Assert.InRange(result.LowerBound, 0, result.Percentage);
        Assert.InRange(result.UpperBound, result.Percentage, 100);
    }

    [Fact]
    public void Localization_is_complete_in_all_supported_languages()
    {
        var validation = BootLens.Core.Localization.LocalizationService.Validate();
        Assert.True(validation.IsComplete, string.Join(Environment.NewLine, validation.MissingByLanguage.Select(pair => $"{pair.Key}: {string.Join(",", pair.Value)}")));
    }

    private static StartupEntry Entry(string id, string path) => new() { Id = id, DisplayName = id, Mechanism = StartupMechanism.RegistryRun, State = StartupState.Enabled, ExecutablePath = path, CommandLine = path };
}
