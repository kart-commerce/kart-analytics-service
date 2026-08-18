using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetCatalogPricingDashboard;

/// <summary>ANL-10: catalog_pricing_dashboard — counts of ProductCreated/ProductPriceChanged/CategoryUpdated in-window.</summary>
public sealed class CatalogPricingDashboardProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "catalog-pricing";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var created = await ingestedEventRepository.GetByTypeInWindowAsync("ProductCreated", windowFrom, windowTo, cancellationToken);
        var priceChanges = await ingestedEventRepository.GetByTypeInWindowAsync("ProductPriceChanged", windowFrom, windowTo, cancellationToken);
        var categoryUpdates = await ingestedEventRepository.GetByTypeInWindowAsync("CategoryUpdated", windowFrom, windowTo, cancellationToken);

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var documentId = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        var document = new CatalogPricingDashboardReadModel
        {
            Id = documentId,
            Granularity = granularityLabel,
            BucketStart = windowFrom.UtcDateTime,
            GeneratedAt = clock.UtcNow.UtcDateTime,
            IsProvisional = isProvisional,
            ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
            ProductsCreated = created.Count,
            PriceChanges = priceChanges.Count,
            CategoryUpdates = categoryUpdates.Count,
        };

        await readModelStore.UpsertAsync("catalog_pricing_dashboard", documentId, document, cancellationToken);
    }
}
