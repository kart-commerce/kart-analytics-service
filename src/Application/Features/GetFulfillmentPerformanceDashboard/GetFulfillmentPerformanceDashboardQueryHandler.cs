using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetFulfillmentPerformanceDashboard;

public sealed class GetFulfillmentPerformanceDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetFulfillmentPerformanceDashboardQuery, FulfillmentPerformanceDashboardResult>
{
    public async Task<FulfillmentPerformanceDashboardResult> Handle(GetFulfillmentPerformanceDashboardQuery request, CancellationToken cancellationToken)
    {
        var granularityLabel = BucketCalculator.ToGranularityLabel(request.Granularity);
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<FulfillmentPerformanceDashboardReadModel>(
            "fulfillment_performance_dashboard",
            q => q.Where(d => d.Granularity == granularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc),
            cancellationToken);

        // Averaging pre-aggregated per-bucket percentiles across multiple buckets is an
        // approximation, not a statistically exact merge (a true merge would need each bucket's
        // raw sample set, which the pre-aggregated-document design deliberately doesn't keep) —
        // documented trade-off, acceptable for a dashboard-level "is this trending up or down"
        // view rather than an exact percentile computation.
        var timeToShip = new DurationPercentiles(
            documents.Count == 0 ? 0 : documents.Average(d => d.TimeToShipP50Hours),
            documents.Count == 0 ? 0 : documents.Average(d => d.TimeToShipP95Hours),
            documents.Count == 0 ? 0 : documents.Average(d => d.TimeToShipP99Hours));

        var timeToDeliver = new DurationPercentiles(
            documents.Count == 0 ? 0 : documents.Average(d => d.TimeToDeliverP50Hours),
            documents.Count == 0 ? 0 : documents.Average(d => d.TimeToDeliverP95Hours),
            documents.Count == 0 ? 0 : documents.Average(d => d.TimeToDeliverP99Hours));

        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new FulfillmentPerformanceDashboardResult(envelope, timeToShip, timeToDeliver);
    }
}
