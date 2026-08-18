using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Domain.Entities;

namespace Kart.Analytics.Application.Features.GetFulfillmentPerformanceDashboard;

/// <summary>
/// ANL-8: fulfillment_performance_dashboard. Time-to-ship = `OrderConfirmed` → `ShipmentDispatched`
/// per `orderId`; time-to-deliver = `ShipmentDispatched` → `OrderDelivered` per `orderId`. Both
/// legs are correlated only within this same recompute window (a same-bucket approximation,
/// documented limitation — a shipment dispatched in a later bucket than its own order-confirmation
/// is not matched here, since that would require an unbounded historical lookback per bucket).
/// </summary>
public sealed class FulfillmentPerformanceDashboardProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "fulfillment-performance";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var confirmed = await ingestedEventRepository.GetByTypeInWindowAsync("OrderConfirmed", windowFrom, windowTo, cancellationToken);
        var dispatched = await ingestedEventRepository.GetByTypeInWindowAsync("ShipmentDispatched", windowFrom, windowTo, cancellationToken);
        var delivered = await ingestedEventRepository.GetByTypeInWindowAsync("OrderDelivered", windowFrom, windowTo, cancellationToken);

        var timeToShipHours = CorrelateHours(confirmed, dispatched);
        var timeToDeliverHours = CorrelateHours(dispatched, delivered);

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var documentId = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        var document = new FulfillmentPerformanceDashboardReadModel
        {
            Id = documentId,
            Granularity = granularityLabel,
            BucketStart = windowFrom.UtcDateTime,
            GeneratedAt = clock.UtcNow.UtcDateTime,
            IsProvisional = isProvisional,
            ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
            TimeToShipP50Hours = PercentileCalculator.Percentile(timeToShipHours, 50),
            TimeToShipP95Hours = PercentileCalculator.Percentile(timeToShipHours, 95),
            TimeToShipP99Hours = PercentileCalculator.Percentile(timeToShipHours, 99),
            TimeToDeliverP50Hours = PercentileCalculator.Percentile(timeToDeliverHours, 50),
            TimeToDeliverP95Hours = PercentileCalculator.Percentile(timeToDeliverHours, 95),
            TimeToDeliverP99Hours = PercentileCalculator.Percentile(timeToDeliverHours, 99),
        };

        await readModelStore.UpsertAsync("fulfillment_performance_dashboard", documentId, document, cancellationToken);
    }

    private static List<double> CorrelateHours(IReadOnlyList<IngestedEvent> startEvents, IReadOnlyList<IngestedEvent> endEvents)
    {
        var endByOrderId = endEvents
            .Select(e => (OrderId: new PayloadReader(e.Payload).GetString("orderId"), e.Envelope.OccurredAt))
            .Where(e => e.OrderId is not null)
            .ToDictionary(e => e.OrderId!, e => e.OccurredAt);

        var hours = new List<double>();
        foreach (var startEvent in startEvents)
        {
            var orderId = new PayloadReader(startEvent.Payload).GetString("orderId");
            if (orderId is not null && endByOrderId.TryGetValue(orderId, out var endedAt))
            {
                hours.Add((endedAt - startEvent.Envelope.OccurredAt).TotalHours);
            }
        }

        hours.Sort();
        return hours;
    }
}
