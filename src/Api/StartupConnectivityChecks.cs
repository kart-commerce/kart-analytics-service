using Confluent.Kafka;
using Kart.Analytics.Infrastructure.Messaging.Kafka;
using Kart.Analytics.Infrastructure.Persistence;
using Kart.Analytics.Infrastructure.Persistence.Mongo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Kart.Analytics.Api;

/// <summary>Verifies every infra dependency is reachable right after boot, one Connecting/connected
/// log pair per dependency, so a misconfigured or unreachable Postgres/Mongo/Kafka shows up
/// immediately in the startup log instead of surfacing later as the first request's failure.</summary>
public static class StartupConnectivityChecks
{
    public static async Task RunAsync(WebApplication app)
    {
        // WebApplicationFactory-based tests (Contract/Integration) run this same Program.cs
        // against real Testcontainers-backed infra of their own, but mark themselves "Testing" so
        // this step — meant for a human watching the real startup log — stays a deliberate no-op.
        if (app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        var logger = app.Logger;

        await CheckAsync(logger, "PostgresDB", async () =>
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            await dbContext.Database.CanConnectAsync();
        });

        await CheckAsync(logger, "MongoDB", async () =>
        {
            using var scope = app.Services.CreateScope();
            var mongoContext = scope.ServiceProvider.GetRequiredService<MongoContext>();
            await mongoContext.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        });

        await CheckAsync(logger, "Kafka", () =>
        {
            var kafkaOptions = app.Services.GetRequiredService<IOptions<KafkaOptions>>().Value;
            using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = kafkaOptions.BootstrapServers }).Build();
            adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        });
    }

    private static async Task CheckAsync(ILogger logger, string dependency, Func<Task> connect)
    {
        logger.LogInformation("Connecting Analytics {Dependency} ...", dependency);
        try
        {
            await connect();
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Failed to connect to Analytics {Dependency}", dependency);
            throw;
        }

        logger.LogInformation("{Dependency} connected", dependency);
    }
}
