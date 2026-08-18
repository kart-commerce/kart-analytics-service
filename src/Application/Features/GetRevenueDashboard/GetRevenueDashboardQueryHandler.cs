using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetRevenueDashboard;

public sealed class GetRevenueDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetRevenueDashboardQuery, RevenueDashboardResult>
{
    public async Task<RevenueDashboardResult> Handle(GetRevenueDashboardQuery request, CancellationToken cancellationToken)
    {
        var granularityLabel = BucketCalculator.ToGranularityLabel(request.Granularity);
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<RevenueDashboardReadModel>(
            "revenue_dashboard",
            q => q.Where(d => d.Granularity == granularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc
                            && d.Sku == request.Sku && d.Category == request.Category)
                  .OrderBy(d => d.BucketStart),
            cancellationToken);

        var series = documents
            .Select(d => new RevenueSeriesPoint(new DateTimeOffset(d.BucketStart, TimeSpan.Zero), new Money(d.RevenueAmount, d.RevenueCurrency), d.OrderCount))
            .ToList();

        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new RevenueDashboardResult(envelope, series);
    }
}
