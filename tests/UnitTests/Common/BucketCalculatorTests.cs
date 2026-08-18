using FluentAssertions;
using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.UnitTests.Common;

public class BucketCalculatorTests
{
    [Theory]
    [InlineData(Granularity.Hour, "2026-08-18T14:00:00+00:00")]
    [InlineData(Granularity.Day, "2026-08-18T00:00:00+00:00")]
    [InlineData(Granularity.Month, "2026-08-01T00:00:00+00:00")]
    public void GetBucketStart_zeroes_the_time_to_the_granularity_boundary(Granularity granularity, string expectedIso)
    {
        var occurredAt = DateTimeOffset.Parse("2026-08-18T14:37:52+00:00");

        var bucketStart = BucketCalculator.GetBucketStart(occurredAt, granularity);

        bucketStart.Should().Be(DateTimeOffset.Parse(expectedIso));
    }

    [Fact]
    public void GetBucketStart_week_aligns_to_monday()
    {
        // 2026-08-18 is a Tuesday.
        var occurredAt = DateTimeOffset.Parse("2026-08-18T14:37:52+00:00");

        var bucketStart = BucketCalculator.GetBucketStart(occurredAt, Granularity.Week);

        bucketStart.DayOfWeek.Should().Be(DayOfWeek.Monday);
        bucketStart.Should().Be(DateTimeOffset.Parse("2026-08-17T00:00:00+00:00"));
    }

    [Fact]
    public void GetBucketWindow_month_spans_the_full_calendar_month_containing_the_date()
    {
        var (from, to) = BucketCalculator.GetBucketWindow(new DateOnly(2026, 8, 17), Granularity.Month);

        from.Should().Be(DateTimeOffset.Parse("2026-08-01T00:00:00+00:00"));
        to.Should().Be(DateTimeOffset.Parse("2026-09-01T00:00:00+00:00"));
    }

    [Fact]
    public void GetBucketWindow_day_spans_exactly_one_day()
    {
        var (from, to) = BucketCalculator.GetBucketWindow(new DateOnly(2026, 8, 17), Granularity.Day);

        (to - from).Should().Be(TimeSpan.FromDays(1));
    }
}
