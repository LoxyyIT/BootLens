namespace BootLens.Core.Domain;

public sealed record StartupMetrics(
    double? CpuMilliseconds,
    double? DiskIoBytes,
    double? PeakMemoryBytes,
    double? DurationMilliseconds,
    int ObservationCount,
    bool WasDelayed);

public sealed record EfficiencyScore(int Value, ScoreConfidence Confidence, string ExplanationKey);

public sealed record TrustSignals(
    bool FilePresent,
    bool IsSigned,
    bool IsMicrosoft,
    bool UnknownPublisher,
    bool UnusualLocation,
    bool IsOrphaned,
    IReadOnlyList<string> SignalKeys);

public sealed record Recommendation(RecommendationKind Kind, string TitleKey, string ExplanationKey);

public sealed record StartupAnalysis(
    EfficiencyScore Efficiency,
    TrustSignals Trust,
    Recommendation Recommendation,
    IReadOnlyList<string> DuplicateEntryIds);

public sealed record BootSummary(
    double? LatestSeconds,
    double? MedianSeconds,
    double? TrendPercent,
    int ActiveEntries,
    int HighImpactEntries,
    int NewEntries,
    int ModifiedEntries);
