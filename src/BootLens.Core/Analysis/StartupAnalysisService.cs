using BootLens.Core.Domain;

namespace BootLens.Core.Analysis;

public sealed class StartupAnalysisService
{
    private readonly StartupScoring _scoring;

    public StartupAnalysisService(StartupScoring scoring)
    {
        _scoring = scoring;
    }

    public StartupAnalysis Analyze(StartupEntry entry, StartupMetrics? metrics = null, IReadOnlyList<StartupEntry>? allEntries = null)
    {
        var effectiveMetrics = metrics ?? new StartupMetrics(entry.CpuMilliseconds, entry.DiskIoBytes, entry.PeakMemoryBytes, null, 0, false);
        var trust = BuildTrustSignals(entry);
        var score = _scoring.Calculate(effectiveMetrics);
        if (score.Value == 0 && score.Confidence == ScoreConfidence.Low) score = EstimateWithoutMeasurements(entry, trust);
        var recommendation = BuildRecommendation(entry, score, trust);
        var duplicates = allEntries?
            .Where(candidate => candidate.Id != entry.Id && SameExecutable(candidate, entry))
            .Select(candidate => candidate.Id)
            .ToArray() ?? [];
        return new StartupAnalysis(score, trust, recommendation, duplicates);
    }

    private static EfficiencyScore EstimateWithoutMeasurements(StartupEntry entry, TrustSignals trust)
    {
        var value = entry.Mechanism switch
        {
            StartupMechanism.Service => 84,
            StartupMechanism.ScheduledTask => 72,
            StartupMechanism.StartupFolder => 68,
            StartupMechanism.RegistryRunOnce => 64,
            _ => 76
        };
        if (entry.IsMicrosoft || entry.IsCritical) value += 12;
        if (trust.IsOrphaned) value -= 38;
        if (trust.UnknownPublisher) value -= 8;
        if (!trust.IsSigned) value -= 5;
        return new EfficiencyScore(Math.Clamp(value, 10, 98), ScoreConfidence.Low, "ScoreEstimated");
    }

    private static TrustSignals BuildTrustSignals(StartupEntry entry)
    {
        var exists = !string.IsNullOrWhiteSpace(entry.ExecutablePath) && File.Exists(entry.ExecutablePath);
        var unusual = entry.ExecutablePath is not null && (entry.ExecutablePath.Contains("\\Temp\\", StringComparison.OrdinalIgnoreCase) || entry.ExecutablePath.Contains("\\Downloads\\", StringComparison.OrdinalIgnoreCase));
        var signals = new List<string>();
        if (entry.IsSigned) signals.Add("PublisherVerified");
        if (!entry.IsSigned && !string.IsNullOrWhiteSpace(entry.Publisher)) signals.Add("SignatureUnverified");
        if (string.IsNullOrWhiteSpace(entry.Publisher)) signals.Add("UnknownPublisher");
        if (!exists) signals.Add("MissingExecutable");
        if (unusual) signals.Add("UnusualLocation");
        if (entry.IsOrphaned()) signals.Add("OrphanedEntry");
        return new TrustSignals(exists, entry.IsSigned, entry.IsMicrosoft, string.IsNullOrWhiteSpace(entry.Publisher), unusual, entry.IsOrphaned(), signals);
    }

    private static Recommendation BuildRecommendation(StartupEntry entry, EfficiencyScore score, TrustSignals trust)
    {
        if (entry.IsCritical || entry.IsMicrosoft) return new Recommendation(RecommendationKind.ProtectedSystemComponent, "ProtectedSystemComponent", "ProtectedSystemComponentExplanation");
        if (trust.IsOrphaned || !trust.FilePresent) return new Recommendation(RecommendationKind.NeedsReview, "NeedsReview", "MissingExecutableExplanation");
        if (score.Confidence == ScoreConfidence.Low) return new Recommendation(RecommendationKind.InsufficientInformation, "InsufficientInformation", "InsufficientInformationExplanation");
        if (score.Value < 45) return new Recommendation(RecommendationKind.HighImpact, "HighImpact", "HighImpactExplanation");
        if (entry.Mechanism is StartupMechanism.RegistryRunOnce or StartupMechanism.ScheduledTask) return new Recommendation(RecommendationKind.UsuallyOptional, "UsuallyOptional", "UsuallyOptionalExplanation");
        return new Recommendation(RecommendationKind.KeepEnabled, "KeepEnabled", "KeepEnabledExplanation");
    }

    private static bool SameExecutable(StartupEntry left, StartupEntry right) =>
        !string.IsNullOrWhiteSpace(left.ExecutablePath) && string.Equals(left.ExecutablePath, right.ExecutablePath, StringComparison.OrdinalIgnoreCase);
}

file static class StartupEntryExtensions
{
    public static bool IsOrphaned(this StartupEntry entry) => entry.IsBroken || (!string.IsNullOrWhiteSpace(entry.ExecutablePath) && !File.Exists(entry.ExecutablePath));
}
