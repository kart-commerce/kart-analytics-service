using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetProductPerformanceDashboard;

/// <summary>database-design.md `product_performance_dashboard` (11th read model, added for
/// `kart-ai-assistant-service`'s product-ranking capability) — backs
/// `GET /internal/v1/dashboards/product-performance`. One document per
/// `(granularity, bucketStart, sku)`; unlike `revenue_dashboard`'s optional `sku?`, `Sku` here is
/// always a real value, since the ranking query needs one row per candidate product.</summary>
public sealed class ProductPerformanceReadModel : BucketedReadModelBase
{
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Always null for v1 — a documented limitation, not an oversight, mirroring
    /// `RevenueDashboardReadModel.Category`'s own precedent exactly: `OrderCreated.items`'
    /// confirmed shape (`{sku, qty, unitPrice: {amount, currency}}`, database-design.md) carries
    /// no category field, and no other already-ingested event type this service's projectors read
    /// resolves a SKU-to-category mapping either — `ProductCreated` carries `categoryId`
    /// (event-contract.md), but no projector in this codebase (including
    /// `CatalogPricingDashboardProjector`, the one other projector that touches
    /// category-adjacent events) joins across event types to turn that into a per-SKU category
    /// lookup; inventing that join here would be a new cross-event-type mechanism this task was
    /// explicitly scoped not to introduce. Populated once a future publisher contract (or an
    /// explicit SKU/category lookup) adds that detail — same "per-SKU documents/fields are written
    /// only when a future publisher contract adds that detail" resolution `revenue_dashboard`
    /// already documents for its own `Sku`/`Category` fields.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// `Money` split into flat `RevenueAmount`/`RevenueCurrency` fields rather than a nested
    /// `revenue: {amount, currency}` sub-document — matches the established convention every other
    /// Money-bearing read model in this codebase already uses (`RevenueDashboardReadModel`,
    /// `PromotionsEffectivenessDashboardReadModel`), even though database-design.md/api-contract.yaml
    /// describe the API-level shape as nested (the query handler reassembles `Money` for the
    /// response, same as `GetRevenueDashboardQueryHandler` already does). Index declarations
    /// against this field (`MongoIndexInitializerHostedService`) use the strongly-typed
    /// `Builders&lt;T&gt;.IndexKeys` lambda form, so they stay correct regardless of the underlying
    /// BSON element name.
    /// </summary>
    public decimal RevenueAmount { get; set; }

    public string RevenueCurrency { get; set; } = "USD";

    public long UnitsSold { get; set; }

    public long OrderCount { get; set; }
}
