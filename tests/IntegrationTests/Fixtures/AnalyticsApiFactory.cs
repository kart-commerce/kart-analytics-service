using Kart.Analytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Kafka;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kart.Analytics.IntegrationTests.Fixtures;

/// <summary>
/// Boots the real ASP.NET Core host against REAL Testcontainers-backed Postgres, MongoDB, and
/// Kafka — "test like a real user, using real DBs," not mocks (mirrors
/// kart-recommendation-service's own `RecommendationApiFactory`). Migrations are applied against
/// the real Postgres container on start-up so every test runs against the actual schema, not an
/// in-memory substitute.
/// </summary>
public sealed class AnalyticsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlBuilder _postgresBuilder = new PostgreSqlBuilder().WithImage("postgres:16-alpine").WithDatabase("kart_analytics_test");
    private PostgreSqlContainer _postgres = null!;
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7").Build();
    private readonly KafkaContainer _kafka = new KafkaBuilder().WithImage("confluentinc/cp-kafka:7.7.0").Build();

    private string? _globalConfigPath;

    public string MongoDatabaseName { get; } = "kart_analytics_test";
    public string KafkaBootstrapServers => _kafka.GetBootstrapAddress();

    public async Task InitializeAsync()
    {
        _postgres = _postgresBuilder.Build();
        await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync(), _kafka.StartAsync());

        _globalConfigPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(_globalConfigPath, "{\"Global\":{},\"Services\":{\"kart-analytics-service\":{}}}");

        Environment.SetEnvironmentVariable("GlobalConfig__Path", _globalConfigPath);
        Environment.SetEnvironmentVariable("ConnectionStrings__AnalyticsDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Mongo__ConnectionString", _mongo.GetConnectionString());
        Environment.SetEnvironmentVariable("Mongo__Database", MongoDatabaseName);
        Environment.SetEnvironmentVariable("Kafka__BootstrapServers", KafkaBootstrapServers);

        // Forces the host (and its hosted services - Kafka consumer, Mongo index initializer,
        // scheduled jobs) to actually start now, with every environment variable above in place.
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
            // Swaps real JWKS/JwtBearer validation for the header-driven fake (see
            // TestAuthHandler's remarks) - AddAuthentication's later call here overrides
            // Program.cs's own DefaultScheme, matching kart-recommendation-service's working pattern.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _mongo.DisposeAsync();
        await _kafka.DisposeAsync();
        if (_globalConfigPath is not null && File.Exists(_globalConfigPath))
        {
            File.Delete(_globalConfigPath);
        }
    }
}

[CollectionDefinition("AnalyticsApi")]
public sealed class AnalyticsApiCollection : ICollectionFixture<AnalyticsApiFactory>;
