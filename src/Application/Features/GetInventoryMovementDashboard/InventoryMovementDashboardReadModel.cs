using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetInventoryMovementDashboard;

/// <summary>database-design.md `inventory_movement_dashboard` — backs `GET /internal/v1/dashboards/inventory-movement` (ANL-9).</summary>
public sealed class InventoryMovementDashboardReadModel : BucketedReadModelBase
{
    /// <summary>Null for the bucket-wide total document; a real SKU for a per-SKU document.</summary>
    public string? Sku { get; set; }
    public long Reserved { get; set; }
    public long ReservationFailed { get; set; }
    public long Released { get; set; }
    public long Replenished { get; set; }
}
