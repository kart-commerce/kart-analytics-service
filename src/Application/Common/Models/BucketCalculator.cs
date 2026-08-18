namespace Kart.Analytics.Application.Common.Models;

/// <summary>Pure time-bucketing logic shared by every projector — given a raw event's own
/// event-time and a granularity, computes which bucket it belongs to.</summary>
public static class BucketCalculator
{
    public static DateTimeOffset GetBucketStart(DateTimeOffset occurredAt, Granularity granularity) => granularity switch
    {
        Granularity.Hour => new DateTimeOffset(occurredAt.Year, occurredAt.Month, occurredAt.Day, occurredAt.Hour, 0, 0, occurredAt.Offset),
        Granularity.Day => new DateTimeOffset(occurredAt.Year, occurredAt.Month, occurredAt.Day, 0, 0, 0, occurredAt.Offset),
        Granularity.Week => GetWeekStart(occurredAt),
        Granularity.Month => new DateTimeOffset(occurredAt.Year, occurredAt.Month, 1, 0, 0, 0, occurredAt.Offset),
        _ => throw new ArgumentOutOfRangeException(nameof(granularity), granularity, null),
    };

    /// <summary>ISO-8601 week start (Monday), matching the platform's other week-bucketed reporting conventions.</summary>
    private static DateTimeOffset GetWeekStart(DateTimeOffset occurredAt)
    {
        var dayOfWeek = (int)occurredAt.DayOfWeek == 0 ? 7 : (int)occurredAt.DayOfWeek; // Sunday=0 -> 7
        var daysSinceMonday = dayOfWeek - 1;
        var dayStart = new DateTimeOffset(occurredAt.Year, occurredAt.Month, occurredAt.Day, 0, 0, 0, occurredAt.Offset);
        return dayStart.AddDays(-daysSinceMonday);
    }

    public static string ToGranularityLabel(Granularity granularity) => granularity.ToString().ToLowerInvariant();

    /// <summary>The full bucket window (start inclusive, end exclusive) that contains
    /// <paramref name="date"/> at the given granularity — e.g. for `Week`, the entire ISO week
    /// containing that date, not just that one day. A correct nightly recompute of a week/month
    /// bucket must span its whole period, not just the day being reconciled.</summary>
    public static (DateTimeOffset From, DateTimeOffset To) GetBucketWindow(DateOnly date, Granularity granularity)
    {
        var anyTimeOnDate = new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero);
        var from = GetBucketStart(anyTimeOnDate, granularity);
        var to = granularity switch
        {
            Granularity.Hour => from.AddHours(1),
            Granularity.Day => from.AddDays(1),
            Granularity.Week => from.AddDays(7),
            Granularity.Month => from.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(granularity), granularity, null),
        };
        return (from, to);
    }
}
