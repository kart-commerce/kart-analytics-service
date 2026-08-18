using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetUserGrowthDashboard;

/// <summary>database-design.md `user_growth_dashboard` — backs `GET /internal/v1/dashboards/user-growth` (ANL-12).</summary>
public sealed class UserGrowthDashboardReadModel : BucketedReadModelBase
{
    public long Signups { get; set; }
    public long SessionsCreated { get; set; }
    public long ProfileChanges { get; set; }
}
