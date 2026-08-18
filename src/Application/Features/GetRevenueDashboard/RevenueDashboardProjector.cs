using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetRevenueDashboard;

/// <summary>ANL-7: revenue_dashboard, recomputed from `OrderCreated.total` in-window.</summary>
public sealed class RevenueDashboardProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "revenue";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var orders = await ingestedEventRepository.GetByTypeInWindowAsync("OrderCreated", windowFrom, windowTo, cancellationToken);

        var totalRevenue = 0m;
        foreach (var order in orders)
        {
            totalRevenue += new PayloadReader(order.Payload).GetDecimal("total");
        }

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var documentId = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        var document = new RevenueDashboardReadModel
        {
            Id = documentId,
            Granularity = granularityLabel,
            BucketStart = windowFrom.UtcDateTime,
            GeneratedAt = clock.UtcNow.UtcDateTime,
            IsProvisional = isProvisional,
            ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
            Sku = null,
            Category = null,
            RevenueAmount = totalRevenue,
            RevenueCurrency = "USD",
            OrderCount = orders.Count,
        };

        await readModelStore.UpsertAsync("revenue_dashboard", documentId, document, cancellationToken);
    }
}
