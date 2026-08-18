using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetAdminAuditDashboard;

/// <summary>
/// database-design.md `admin_audit_log` — backs `GET /internal/v1/dashboards/admin-audit`
/// (ANL-14). A log, not a time-bucket aggregate (this endpoint takes no `granularity` param) —
/// deliberately does not extend <see cref="BucketedReadModelBase"/>. <c>Id</c> is the source
/// `event_id` itself, giving idempotent upsert for free without a separate unique index.
/// </summary>
public sealed class AdminAuditLogReadModel : ReadModelBase
{
    public DateTime OccurredAt { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string AdminId { get; set; } = string.Empty;
}
