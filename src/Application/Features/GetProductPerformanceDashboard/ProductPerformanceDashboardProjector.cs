using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Domain.Entities;

namespace Kart.Analytics.Application.Features.GetProductPerformanceDashboard;

/// <summary>
/// requirement-spec.md §6 item 7 / ddd-model.md Modeling Decision 6: product_performance_dashboard,
/// recomputed per-SKU from `OrderCreated.items` in-window — the same "full recompute from raw
/// storage, never incrementally mutated" idempotency treatment every other projector already
/// applies (design-decisions.md "Idempotency Mechanism for Replay-Safe Aggregation"). Unlike
/// `revenue_dashboard` (which only ever populates its bucket-wide total document, `Sku`/`Category`
/// left null, because `OrderCreated`'s payload was not yet specified with a decomposed per-line-item
/// breakdown when that projector was built), this projector relies on the now-confirmed
/// `OrderLineItem` shape `{sku, qty, unitPrice: {amount, currency}}` (database-design.md, checked
/// against `kart-order-service`'s own `ddd-model.md`/`database-design.md`) to write one document
/// per candidate SKU.
/// </summary>
public sealed class ProductPerformanceDashboardProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "product-performance";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var orders = await ingestedEventRepository.GetByTypeInWindowAsync("OrderCreated", windowFrom, windowTo, cancellationToken);

        var bySku = new Dictionary<string, SkuAggregate>();

        foreach (var order in orders)
        {
            var reader = new PayloadReader(order.Payload);
            var skusInThisOrder = new HashSet<string>();

            foreach (var item in reader.GetArray("items"))
            {
                var sku = item.GetString("sku");
                if (sku is null)
                {
                    continue; // Schema Evolution tolerance (edge-cases.md): a malformed line item degrades this one line, not the whole recompute.
                }

                var qty = (long)item.GetDecimal("qty");
                var unitPrice = item.GetObject("unitPrice");
                var unitAmount = unitPrice?.GetDecimal("amount") ?? 0m;
                var unitCurrency = unitPrice?.GetString("currency") ?? "USD";

                if (!bySku.TryGetValue(sku, out var aggregate))
                {
                    aggregate = new SkuAggregate(unitCurrency);
                }

                aggregate.RevenueAmount += unitAmount * qty;
                aggregate.UnitsSold += qty;
                bySku[sku] = aggregate;

                skusInThisOrder.Add(sku);
            }

            // orderCount is "how many orders contain this SKU at least once," not a per-line-item
            // count — one order with the same SKU split across two line items still counts once.
            foreach (var sku in skusInThisOrder)
            {
                var aggregate = bySku[sku];
                aggregate.OrderCount += 1;
                bySku[sku] = aggregate;
            }
        }

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var bucketPrefix = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        foreach (var (sku, aggregate) in bySku)
        {
            var documentId = $"{bucketPrefix}:{sku}";

            var document = new ProductPerformanceReadModel
            {
                Id = documentId,
                Granularity = granularityLabel,
                BucketStart = windowFrom.UtcDateTime,
                GeneratedAt = clock.UtcNow.UtcDateTime,
                IsProvisional = isProvisional,
                ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
                Sku = sku,
                Category = null, // Documented limitation — see ProductPerformanceReadModel.Category.
                RevenueAmount = aggregate.RevenueAmount,
                RevenueCurrency = aggregate.Currency,
                UnitsSold = aggregate.UnitsSold,
                OrderCount = aggregate.OrderCount,
            };

            await readModelStore.UpsertAsync("product_performance_dashboard", documentId, document, cancellationToken);
        }
    }

    private sealed class SkuAggregate(string currency)
    {
        public decimal RevenueAmount { get; set; }
        public long UnitsSold { get; set; }
        public long OrderCount { get; set; }
        public string Currency { get; } = currency;
    }
}
