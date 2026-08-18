using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetOrderConversionFunnel;

/// <summary>ANL-6: order_conversion_funnel — funnel stage counts from CartCheckedOut through
/// OrderDelivered (api-contract.yaml's `stage` enum), recomputed from raw storage each run.</summary>
public sealed class OrderConversionFunnelProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "order-conversion-funnel";

    private static readonly string[] StageEventTypes = ["CartCheckedOut", "OrderCreated", "OrderConfirmed", "PaymentCompleted", "OrderDelivered"];

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var counts = new List<long>(StageEventTypes.Length);
        foreach (var eventType in StageEventTypes)
        {
            var events = await ingestedEventRepository.GetByTypeInWindowAsync(eventType, windowFrom, windowTo, cancellationToken);
            counts.Add(events.Count);
        }

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var documentId = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        var document = new OrderConversionFunnelReadModel
        {
            Id = documentId,
            Granularity = granularityLabel,
            BucketStart = windowFrom.UtcDateTime,
            GeneratedAt = clock.UtcNow.UtcDateTime,
            IsProvisional = isProvisional,
            ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
            Stages = Enumerable.Range(0, StageEventTypes.Length).Select(i => new FunnelStageReadModel
            {
                Stage = StageEventTypes[i],
                Count = counts[i],
                DropOffRate = i == 0 || counts[i - 1] == 0 ? null : 1.0 - (double)counts[i] / counts[i - 1],
            }).ToList(),
        };

        await readModelStore.UpsertAsync("order_conversion_funnel", documentId, document, cancellationToken);
    }
}
