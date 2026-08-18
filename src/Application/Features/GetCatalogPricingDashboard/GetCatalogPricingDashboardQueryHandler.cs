using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetCatalogPricingDashboard;

public sealed class GetCatalogPricingDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetCatalogPricingDashboardQuery, CatalogPricingDashboardResult>
{
    public async Task<CatalogPricingDashboardResult> Handle(GetCatalogPricingDashboardQuery request, CancellationToken cancellationToken)
    {
        var granularityLabel = BucketCalculator.ToGranularityLabel(request.Granularity);
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<CatalogPricingDashboardReadModel>(
            "catalog_pricing_dashboard",
            q => q.Where(d => d.Granularity == granularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc),
            cancellationToken);

        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new CatalogPricingDashboardResult(
            envelope,
            documents.Sum(d => d.ProductsCreated),
            documents.Sum(d => d.PriceChanges),
            documents.Sum(d => d.CategoryUpdates));
    }
}
