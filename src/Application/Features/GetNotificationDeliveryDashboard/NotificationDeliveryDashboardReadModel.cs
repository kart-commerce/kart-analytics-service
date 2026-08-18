using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetNotificationDeliveryDashboard;

/// <summary>database-design.md `notification_delivery_dashboard` — backs `GET /internal/v1/dashboards/notification-delivery` (ANL-15).</summary>
public sealed class NotificationDeliveryDashboardReadModel : BucketedReadModelBase
{
    /// <summary>Null for the bucket-wide total document; a real channel for a per-channel document.</summary>
    public string? Channel { get; set; }
    public long Sent { get; set; }
    public long PriceAlertsTriggered { get; set; }
}
