using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetCatalogPricingDashboard;

/// <summary>database-design.md `catalog_pricing_dashboard` — backs `GET /internal/v1/dashboards/catalog-pricing` (ANL-10).</summary>
public sealed class CatalogPricingDashboardReadModel : BucketedReadModelBase
{
    public long ProductsCreated { get; set; }
    public long PriceChanges { get; set; }
    public long CategoryUpdates { get; set; }
}
