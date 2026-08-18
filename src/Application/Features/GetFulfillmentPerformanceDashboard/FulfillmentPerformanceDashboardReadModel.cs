using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetFulfillmentPerformanceDashboard;

/// <summary>database-design.md `fulfillment_performance_dashboard` — backs `GET /internal/v1/dashboards/fulfillment-performance` (ANL-8).</summary>
public sealed class FulfillmentPerformanceDashboardReadModel : BucketedReadModelBase
{
    public double TimeToShipP50Hours { get; set; }
    public double TimeToShipP95Hours { get; set; }
    public double TimeToShipP99Hours { get; set; }
    public double TimeToDeliverP50Hours { get; set; }
    public double TimeToDeliverP95Hours { get; set; }
    public double TimeToDeliverP99Hours { get; set; }
}
