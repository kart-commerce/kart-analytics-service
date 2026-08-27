using FluentAssertions;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Features.GetProductPerformanceDashboard;
using NSubstitute;

namespace Kart.Analytics.UnitTests.Features.GetProductPerformanceDashboard;

/// <summary>
/// Focused coverage for edge-cases.md "Ranking Ties at the Limit Cutoff (product-performance)":
/// the requested metric is always the primary sort key, with `sku` ascending appended as a
/// deterministic secondary sort key, so a tie exactly at the `limit` cutoff always resolves the
/// same way. Aggregation correctness (`OrderCreated` → per-SKU totals) is covered separately by
/// <see cref="ProductPerformanceDashboardProjectorTests"/>.
/// </summary>
public sealed class GetProductPerformanceDashboardQueryHandlerTests
{
    private readonly IReadModelStore _readModelStore = Substitute.For<IReadModelStore>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly GetProductPerformanceDashboardQueryHandler _handler;

    public GetProductPerformanceDashboardQueryHandlerTests()
    {
        _clock.UtcNow.Returns(DateTimeOffset.Parse("2026-08-18T01:00:00Z"));
        _handler = new GetProductPerformanceDashboardQueryHandler(_readModelStore, _clock);
    }

    private void SeedDocuments(IReadOnlyList<ProductPerformanceReadModel> documents)
    {
        // Mirrors MongoReadModelStore's real contract: the handler's filter/sort delegate is run
        // against whatever documents this fake collection holds, exactly as the real Mongo LINQ
        // provider would run it server-side.
        _readModelStore.QueryAsync<ProductPerformanceReadModel>(
                Arg.Any<string>(),
                Arg.Any<Func<IQueryable<ProductPerformanceReadModel>, IQueryable<ProductPerformanceReadModel>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var query = callInfo.ArgAt<Func<IQueryable<ProductPerformanceReadModel>, IQueryable<ProductPerformanceReadModel>>>(1);
                return Task.FromResult(query(documents.AsQueryable()).ToList());
            });
    }

    private static ProductPerformanceReadModel Doc(string sku, decimal revenue, long unitsSold, long orderCount) => new()
    {
        Id = $"day:2026-08-17T00:{sku}",
        Granularity = "day",
        BucketStart = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
        GeneratedAt = DateTime.UtcNow,
        IsProvisional = true,
        ReconciledThrough = null,
        Sku = sku,
        Category = null,
        RevenueAmount = revenue,
        RevenueCurrency = "USD",
        UnitsSold = unitsSold,
        OrderCount = orderCount,
    };

    [Fact]
    public async Task Handle_breaks_a_tie_at_the_limit_cutoff_by_sku_ascending()
    {
        // SKU-B, SKU-A, SKU-E all tie at revenue=50 — only two of the three can fit under
        // limit=3 once SKU-C's higher revenue takes the first slot. Sku-ascending must pick
        // SKU-A and SKU-B over SKU-E, deterministically, every time.
        SeedDocuments([
            Doc("SKU-C", revenue: 100m, unitsSold: 1, orderCount: 1),
            Doc("SKU-E", revenue: 50m, unitsSold: 1, orderCount: 1),
            Doc("SKU-B", revenue: 50m, unitsSold: 1, orderCount: 1),
            Doc("SKU-A", revenue: 50m, unitsSold: 1, orderCount: 1),
            Doc("SKU-D", revenue: 10m, unitsSold: 1, orderCount: 1),
        ]);

        var query = new GetProductPerformanceDashboardQuery(
            DateTimeOffset.Parse("2026-08-17T00:00:00Z"), DateTimeOffset.Parse("2026-08-18T00:00:00Z"), "revenue", Category: null, "desc", Limit: 3);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Products.Select(p => p.Sku).Should().Equal("SKU-C", "SKU-A", "SKU-B");
    }

    [Fact]
    public async Task Handle_repeated_calls_against_unchanged_data_return_the_identical_order()
    {
        SeedDocuments([
            Doc("SKU-B", revenue: 50m, unitsSold: 1, orderCount: 1),
            Doc("SKU-A", revenue: 50m, unitsSold: 1, orderCount: 1),
        ]);

        var query = new GetProductPerformanceDashboardQuery(
            DateTimeOffset.Parse("2026-08-17T00:00:00Z"), DateTimeOffset.Parse("2026-08-18T00:00:00Z"), "revenue", Category: null, "desc", Limit: 10);

        var first = await _handler.Handle(query, CancellationToken.None);
        var second = await _handler.Handle(query, CancellationToken.None);

        first.Products.Select(p => p.Sku).Should().Equal(second.Products.Select(p => p.Sku));
        first.Products.Select(p => p.Sku).Should().Equal("SKU-A", "SKU-B");
    }

    [Fact]
    public async Task Handle_ranks_by_units_sold_when_that_is_the_requested_metric()
    {
        SeedDocuments([
            Doc("SKU-A", revenue: 999m, unitsSold: 1, orderCount: 1), // Highest revenue, lowest units.
            Doc("SKU-B", revenue: 1m, unitsSold: 100, orderCount: 1), // Lowest revenue, highest units.
        ]);

        var query = new GetProductPerformanceDashboardQuery(
            DateTimeOffset.Parse("2026-08-17T00:00:00Z"), DateTimeOffset.Parse("2026-08-18T00:00:00Z"), "units_sold", Category: null, "desc", Limit: 10);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Products.Select(p => p.Sku).Should().Equal("SKU-B", "SKU-A");
    }

    [Fact]
    public async Task Handle_direction_asc_ranks_lowest_first_but_still_breaks_ties_by_sku_ascending()
    {
        SeedDocuments([
            Doc("SKU-C", revenue: 10m, unitsSold: 1, orderCount: 1),
            Doc("SKU-B", revenue: 5m, unitsSold: 1, orderCount: 1),
            Doc("SKU-A", revenue: 5m, unitsSold: 1, orderCount: 1),
        ]);

        var query = new GetProductPerformanceDashboardQuery(
            DateTimeOffset.Parse("2026-08-17T00:00:00Z"), DateTimeOffset.Parse("2026-08-18T00:00:00Z"), "revenue", Category: null, "asc", Limit: 10);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Products.Select(p => p.Sku).Should().Equal("SKU-A", "SKU-B", "SKU-C");
    }

    [Fact]
    public async Task Handle_returns_exactly_limit_rows_and_all_three_metrics_per_entry()
    {
        SeedDocuments([
            Doc("SKU-A", revenue: 30m, unitsSold: 3, orderCount: 2),
            Doc("SKU-B", revenue: 5m, unitsSold: 1, orderCount: 1),
            Doc("SKU-C", revenue: 1m, unitsSold: 1, orderCount: 1),
        ]);

        var query = new GetProductPerformanceDashboardQuery(
            DateTimeOffset.Parse("2026-08-17T00:00:00Z"), DateTimeOffset.Parse("2026-08-18T00:00:00Z"), "revenue", Category: null, "desc", Limit: 1);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Products.Should().HaveCount(1);
        var top = result.Products.Single();
        top.Sku.Should().Be("SKU-A");
        top.Revenue.Amount.Should().Be(30m);
        top.UnitsSold.Should().Be(3);
        top.OrderCount.Should().Be(2);
    }
}
