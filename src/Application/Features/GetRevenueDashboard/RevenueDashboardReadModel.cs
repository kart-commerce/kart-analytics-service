using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetRevenueDashboard;

/// <summary>database-design.md `revenue_dashboard` — backs `GET /internal/v1/dashboards/revenue` (ANL-7).</summary>
public sealed class RevenueDashboardReadModel : BucketedReadModelBase
{
    /// <summary>Null for the bucket-wide total document; a real SKU value for a per-SKU document
    /// (documented limitation: `OrderCreated`'s payload — orderId, userId, items, total — is not
    /// specified with a decomposed per-line-item SKU/price breakdown, so only the bucket-wide
    /// total is populated for v1; per-SKU documents are written only when a future publisher
    /// contract adds that detail).</summary>
    public string? Sku { get; set; }
    public string? Category { get; set; }
    public decimal RevenueAmount { get; set; }
    public string RevenueCurrency { get; set; } = "USD";
    public long OrderCount { get; set; }
}
