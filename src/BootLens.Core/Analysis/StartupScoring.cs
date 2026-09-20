using BootLens.Core.Domain;

namespace BootLens.Core.Analysis;

public sealed class StartupScoring
{
    public EfficiencyScore Calculate(StartupMetrics metrics)
    {
        var observations = Math.Max(metrics.ObservationCount, 0);
        if (observations == 0 || (metrics.CpuMilliseconds is null && metrics.DiskIoBytes is null && metrics.PeakMemoryBytes is null && metrics.DurationMilliseconds is null))
        {
            return new EfficiencyScore(0, ScoreConfidence.Low, "ScoreNotMeasured");
        }

        var cpuPenalty = Math.Clamp((metrics.CpuMilliseconds ?? 0) / 1000d, 0, 35);
        var ioPenalty = Math.Clamp((metrics.DiskIoBytes ?? 0) / (50 * 1024 * 1024d), 0, 25);
        var memoryPenalty = Math.Clamp((metrics.PeakMemoryBytes ?? 0) / (1024 * 1024 * 1024d), 0, 20);
        var durationPenalty = Math.Clamp((metrics.DurationMilliseconds ?? 0) / 1000d, 0, 20);
        var delayBonus = metrics.WasDelayed ? 4 : 0;
        var value = (int)Math.Round(Math.Clamp(100 - cpuPenalty - ioPenalty - memoryPenalty - durationPenalty + delayBonus, 0, 100));
        var confidence = observations >= 10 ? ScoreConfidence.High : observations >= 3 ? ScoreConfidence.Medium : ScoreConfidence.Low;
        return new EfficiencyScore(value, confidence, "ScoreMeasured");
    }
}
