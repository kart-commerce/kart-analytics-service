using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kart.Analytics.Infrastructure.Persistence;

/// <summary>
/// Design-time-only factory `dotnet ef migrations add`/`database update` use to build
/// <see cref="AnalyticsDbContext"/> without spinning up the full Api host — mirrors
/// kart-identity-service's <c>IdentityDbContextFactory</c> exactly. Never used at runtime; the
/// app's own DI registration (Infrastructure/DependencyInjection.cs) takes over there.
/// </summary>
public sealed class AnalyticsDbContextFactory : IDesignTimeDbContextFactory<AnalyticsDbContext>
{
    public AnalyticsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ANALYTICS_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=kart_analytics;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AnalyticsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AnalyticsDbContext(optionsBuilder.Options);
    }
}
