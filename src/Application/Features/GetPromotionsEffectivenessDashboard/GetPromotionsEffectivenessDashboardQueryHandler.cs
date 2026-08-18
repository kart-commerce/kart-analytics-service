using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetPromotionsEffectivenessDashboard;

public sealed class GetPromotionsEffectivenessDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetPromotionsEffectivenessDashboardQuery, PromotionsEffectivenessDashboardResult>
{
    public async Task<PromotionsEffectivenessDashboardResult> Handle(GetPromotionsEffectivenessDashboardQuery request, CancellationToken cancellationToken)
    {
        var granularityLabel = BucketCalculator.ToGranularityLabel(request.Granularity);
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<PromotionsEffectivenessDashboardReadModel>(
            "promotions_effectiveness_dashboard",
            q => q.Where(d => d.Granularity == granularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc),
            cancellationToken);

        var couponsRedeemed = documents.Sum(d => d.CouponsRedeemed);
        var quotesIssued = documents.Sum(d => d.QuotesIssued);
        var attributableVolume = documents.Sum(d => d.AttributableOrderVolumeAmount);
        var redemptionRate = quotesIssued == 0 ? 0d : (double)couponsRedeemed / quotesIssued;

        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new PromotionsEffectivenessDashboardResult(envelope, couponsRedeemed, quotesIssued, new Money(attributableVolume, "USD"), redemptionRate);
    }
}
