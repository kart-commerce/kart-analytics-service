using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetProductPerformanceDashboard;

/// <summary>
/// api-contract.yaml's `product-performance` endpoint has no `granularity` query parameter, unlike
/// every other bucketed dashboard — the ranking query works over an arbitrary `from`/`to` window,
/// not a caller-chosen bucket size. This handler always reads the `day`-granularity bucket
/// documents covering the window (`RunIncrementalProjection`/`RunNightlyReconciliation` already
/// populate all four granularities for every dashboard regardless of which one a caller later
/// queries, exactly as they do for the ten existing dashboards), then sums each SKU's per-bucket
/// totals across every day-bucket the window spans before ranking. `IReadModelStore` exposes plain
/// LINQ over one collection, not a server-side `$group` pipeline, so summing across multiple bucket
/// documents in application code is the same pattern every other multi-bucket-window handler in
/// this service already uses (e.g. `GetCatalogPricingDashboardQueryHandler`'s `documents.Sum(...)`),
/// just grouped by SKU first here.
/// </summary>
public sealed class GetProductPerformanceDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetProductPerformanceDashboardQuery, ProductPerformanceDashboardResult>
{
    private const string QueryGranularityLabel = "day";

    public async Task<ProductPerformanceDashboardResult> Handle(GetProductPerformanceDashboardQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<ProductPerformanceReadModel>(
            "product_performance_dashboard",
            q => q.Where(d => d.Granularity == QueryGranularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc && d.Category == request.Category),
            cancellationToken);

        var bySku = documents
            .GroupBy(d => d.Sku)
            .Select(group => new SkuTotals(
                group.Key,
                group.Select(d => d.Category).FirstOrDefault(c => c is not null),
                group.Sum(d => d.RevenueAmount),
                group.Select(d => d.RevenueCurrency).FirstOrDefault() ?? "USD",
                group.Sum(d => d.UnitsSold),
                group.Sum(d => d.OrderCount)))
            .ToList();

        // edge-cases.md "Ranking Ties at the Limit Cutoff (product-performance)": the requested
        // metric is always the primary sort key, with `sku` ascending appended as a deterministic
        // secondary sort key — regardless of direction — so a tie at the `limit` cutoff resolves
        // identically on every repeat call against the same underlying data.
        var descending = request.Direction != "asc";
        IOrderedEnumerable<SkuTotals> ranked = request.Metric switch
        {
            "units_sold" => descending ? bySku.OrderByDescending(p => p.UnitsSold).ThenBy(p => p.Sku) : bySku.OrderBy(p => p.UnitsSold).ThenBy(p => p.Sku),
            "order_count" => descending ? bySku.OrderByDescending(p => p.OrderCount).ThenBy(p => p.Sku) : bySku.OrderBy(p => p.OrderCount).ThenBy(p => p.Sku),
            _ => descending ? bySku.OrderByDescending(p => p.RevenueAmount).ThenBy(p => p.Sku) : bySku.OrderBy(p => p.RevenueAmount).ThenBy(p => p.Sku),
        };

        var products = ranked
            .Take(request.Limit)
            .Select(p => new ProductPerformanceEntry(p.Sku, p.Category, new Money(p.RevenueAmount, p.RevenueCurrency), p.UnitsSold, p.OrderCount))
            .ToList();

        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new ProductPerformanceDashboardResult(envelope, products);
    }

    private sealed record SkuTotals(string Sku, string? Category, decimal RevenueAmount, string RevenueCurrency, long UnitsSold, long OrderCount);
}
