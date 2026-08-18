using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetPromotionsEffectivenessDashboard;

/// <summary>database-design.md `promotions_effectiveness_dashboard` — backs `GET /internal/v1/dashboards/promotions-effectiveness` (ANL-11).</summary>
public sealed class PromotionsEffectivenessDashboardReadModel : BucketedReadModelBase
{
    public long CouponsRedeemed { get; set; }
    public long QuotesIssued { get; set; }
    public decimal AttributableOrderVolumeAmount { get; set; }
    public string AttributableOrderVolumeCurrency { get; set; } = "USD";
    public double RedemptionRate { get; set; }
}
