using Kart.Analytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kart.Analytics.ContractTests.Fixtures;

/// <summary>
/// Lighter-weight than IntegrationTests' `AnalyticsApiFactory`: contract tests validate response
/// *shape* against `api-contract.yaml`, not ingestion behavior, so this boots against real
/// Postgres + MongoDB (both wired unconditionally by `AddInfrastructure`) but skips a real Kafka
/// broker — the Kafka consumer hosted service's own connect-retry loop tolerates an unreachable
/// broker without crashing the host (it logs and backs off), which is enough for a short-lived
/// shape-only test run.
/// </summary>
public sealed class AnalyticsContractApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlBuilder _postgresBuilder = new PostgreSqlBuilder().WithImage("postgres:16-alpine").WithDatabase("kart_analytics_contract");
    private PostgreSqlContainer _postgres = null!;
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7").Build();

    private string? _globalConfigPath;

    public async Task InitializeAsync()
    {
        _postgres = _postgresBuilder.Build();
        await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync());

        _globalConfigPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(_globalConfigPath, "{\"Global\":{},\"Services\":{\"kart-analytics-service\":{}}}");

        Environment.SetEnvironmentVariable("GlobalConfig__Path", _globalConfigPath);
        Environment.SetEnvironmentVariable("ConnectionStrings__AnalyticsDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Mongo__ConnectionString", _mongo.GetConnectionString());
        Environment.SetEnvironmentVariable("Mongo__Database", "kart_analytics_contract");
        Environment.SetEnvironmentVariable("Kafka__BootstrapServers", "127.0.0.1:1");

        _ = Server;

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _mongo.DisposeAsync();
        if (_globalConfigPath is not null && File.Exists(_globalConfigPath))
        {
            File.Delete(_globalConfigPath);
        }
    }
}

[CollectionDefinition("AnalyticsContractApi")]
public sealed class AnalyticsContractApiCollection : ICollectionFixture<AnalyticsContractApiFactory>;
