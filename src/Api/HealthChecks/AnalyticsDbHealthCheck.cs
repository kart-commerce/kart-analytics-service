using Kart.Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kart.Analytics.Api.HealthChecks;

/// <summary>Readiness signal for `/health/ready` — a database that is reachable but behind on
/// migrations must fail readiness too, not just an unreachable one, so a pod never accepts
/// ingestion traffic against a schema the app doesn't actually match yet.</summary>
public sealed class AnalyticsDbHealthCheck(AnalyticsDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            return pending.Length == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{pending.Length} pending migration(s): {string.Join(", ", pending)}");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Analytics database is unreachable", exception);
        }
    }
}
