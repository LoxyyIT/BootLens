namespace BootLens.Core.Analysis;

public sealed record CommunityEstimate(double Percentage, double LowerBound, double UpperBound, int Observations, string ConfidenceKey);

public sealed class CommunityConfidence
{
    public CommunityEstimate Estimate(int positive, int observations)
    {
        if (observations <= 0 || positive < 0 || positive > observations) return new CommunityEstimate(0, 0, 0, 0, "NotEnoughCommunityData");
        var n = observations;
        var p = positive / (double)n;
        var z = 1.96;
        var denominator = 1 + z * z / n;
        var center = (p + z * z / (2 * n)) / denominator;
        var margin = z * Math.Sqrt((p * (1 - p) + z * z / (4 * n)) / n) / denominator;
        var confidence = n >= 1000 ? "VeryHigh" : n >= 100 ? "High" : n >= 30 ? "Medium" : "Low";
        return new CommunityEstimate(p * 100, Math.Max(0, center - margin) * 100, Math.Min(1, center + margin) * 100, n, confidence);
    }
}
