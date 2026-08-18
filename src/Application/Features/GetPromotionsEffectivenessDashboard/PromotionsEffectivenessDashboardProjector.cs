using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetPromotionsEffectivenessDashboard;

/// <summary>
/// ANL-11: promotions_effectiveness_dashboard. `attributableOrderVolume` is a best-effort proxy —
/// summed from `PriceQuoteIssued.total` (the quote's own computed total), since no documented
/// event links a redeemed coupon/issued quote directly to its resulting completed order total;
/// a future event-contract addition (e.g. `orderId` on `CouponRedeemed` joined to `OrderCreated`)
/// would let this be computed exactly rather than approximated.
/// </summary>
public sealed class PromotionsEffectivenessDashboardProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "promotions-effectiveness";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var redeemed = await ingestedEventRepository.GetByTypeInWindowAsync("CouponRedeemed", windowFrom, windowTo, cancellationToken);
        var quotesIssued = await ingestedEventRepository.GetByTypeInWindowAsync("PriceQuoteIssued", windowFrom, windowTo, cancellationToken);

        var attributableVolume = quotesIssued.Sum(q => new PayloadReader(q.Payload).GetDecimal("total"));
        var redemptionRate = quotesIssued.Count == 0 ? 0d : (double)redeemed.Count / quotesIssued.Count;

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var documentId = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        var document = new PromotionsEffectivenessDashboardReadModel
        {
            Id = documentId,
            Granularity = granularityLabel,
            BucketStart = windowFrom.UtcDateTime,
            GeneratedAt = clock.UtcNow.UtcDateTime,
            IsProvisional = isProvisional,
            ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
            CouponsRedeemed = redeemed.Count,
            QuotesIssued = quotesIssued.Count,
            AttributableOrderVolumeAmount = attributableVolume,
            AttributableOrderVolumeCurrency = "USD",
            RedemptionRate = redemptionRate,
        };

        await readModelStore.UpsertAsync("promotions_effectiveness_dashboard", documentId, document, cancellationToken);
    }
}
