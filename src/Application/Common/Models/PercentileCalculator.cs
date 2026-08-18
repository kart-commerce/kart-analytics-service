namespace Kart.Analytics.Application.Common.Models;

/// <summary>Nearest-rank percentile over a sample set — used by the fulfillment-performance
/// dashboard's time-to-ship/time-to-deliver p50/p95/p99 (database-design.md).</summary>
public static class PercentileCalculator
{
    public static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        var clampedRank = Math.Clamp(rank, 0, sortedValues.Count - 1);
        return sortedValues[clampedRank];
    }
}
