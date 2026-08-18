using FluentAssertions;
using Kart.Analytics.IntegrationTests.Fixtures;
using Kart.Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainEventId = Kart.Analytics.Domain.ValueObjects.EventId;

namespace Kart.Analytics.IntegrationTests;

/// <summary>
/// "Test like a real user, using real DBs" — every test here produces onto a real Kafka topic
/// (Testcontainers) and asserts against the real Postgres schema the app actually migrated,
/// exercising ANL-1/ANL-2/ANL-4's idempotency, DLQ, and PII-redaction guarantees end-to-end.
/// </summary>
[Collection("AnalyticsApi")]
public sealed class IngestionEndToEndTests(AnalyticsApiFactory factory)
{
    private static async Task<T> WaitForAsync<T>(Func<Task<T?>> probe, TimeSpan timeout) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await probe();
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }

    [Fact]
    public async Task Producing_an_OrderCreated_event_lands_a_row_in_analytics_raw_events()
    {
        var eventId = Guid.NewGuid();
        await KafkaTestProducer.ProduceAsync(
            factory.KafkaBootstrapServers, "kart.order.events", "OrderCreated", "kart-order-service", "order-e2e-1",
            eventId, DateTimeOffset.UtcNow, $"{{\"orderId\":\"order-e2e-1\",\"userId\":\"user-e2e-1\",\"total\":42.50}}");

        var domainEventId = DomainEventId.From(eventId);
        var row = await WaitForAsync(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            return await db.IngestedEvents.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == domainEventId);
        }, TimeSpan.FromSeconds(60));

        row.Envelope.EventType.Should().Be("OrderCreated");
        row.Envelope.PublisherService.Should().Be("kart-order-service");
        row.CreatedBy.Should().Be("system:analytics-ingestion-consumer");
    }

    [Fact]
    public async Task Replaying_the_same_event_id_never_creates_a_duplicate_row()
    {
        var eventId = Guid.NewGuid();
        var domainEventId = DomainEventId.From(eventId);

        async Task ProduceOnce() => await KafkaTestProducer.ProduceAsync(
            factory.KafkaBootstrapServers, "kart.order.events", "OrderCreated", "kart-order-service", "order-e2e-2",
            eventId, DateTimeOffset.UtcNow, "{\"orderId\":\"order-e2e-2\",\"userId\":\"user-e2e-2\",\"total\":10}");

        await ProduceOnce();
        await WaitForAsync(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            return await db.IngestedEvents.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == domainEventId);
        }, TimeSpan.FromSeconds(60));

        // Replay the identical event id twice more.
        await ProduceOnce();
        await ProduceOnce();
        await Task.Delay(TimeSpan.FromSeconds(3));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var matchingRows = await db.IngestedEvents.AsNoTracking().Where(e => e.EventId == domainEventId).ToListAsync();

        matchingRows.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_malformed_message_lands_in_the_dlq_without_stalling_the_next_valid_message()
    {
        await KafkaTestProducer.ProduceMalformedAsync(factory.KafkaBootstrapServers, "kart.order.events");

        var dlqRow = await WaitForAsync(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            return await db.DeadLetteredEvents.AsNoTracking().OrderByDescending(e => e.DlqLandedAt).FirstOrDefaultAsync();
        }, TimeSpan.FromSeconds(60));

        dlqRow.Should().NotBeNull();

        // The partition must keep processing after a poison message — a subsequent valid
        // message on the same topic must still be ingested.
        var followUpEventId = Guid.NewGuid();
        var domainFollowUpEventId = DomainEventId.From(followUpEventId);
        await KafkaTestProducer.ProduceAsync(
            factory.KafkaBootstrapServers, "kart.order.events", "OrderCreated", "kart-order-service", "order-e2e-3",
            followUpEventId, DateTimeOffset.UtcNow, "{\"orderId\":\"order-e2e-3\",\"userId\":\"user-e2e-3\",\"total\":5}");

        await WaitForAsync(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            return await db.IngestedEvents.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == domainFollowUpEventId);
        }, TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task UserDataErased_redacts_pii_fields_on_the_users_prior_events()
    {
        var userId = $"user-e2e-{Guid.NewGuid():N}";
        var registeredEventId = Guid.NewGuid();
        var domainRegisteredEventId = DomainEventId.From(registeredEventId);

        await KafkaTestProducer.ProduceAsync(
            factory.KafkaBootstrapServers, "kart.identity.events", "UserRegistered", "kart-identity-service", userId,
            registeredEventId, DateTimeOffset.UtcNow, $"{{\"userId\":\"{userId}\",\"email\":\"pii-e2e@example.com\"}}");

        await WaitForAsync(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            return await db.IngestedEvents.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == domainRegisteredEventId);
        }, TimeSpan.FromSeconds(60));

        var erasedEventId = Guid.NewGuid();
        await KafkaTestProducer.ProduceAsync(
            factory.KafkaBootstrapServers, "kart.user.events", "UserDataErased", "kart-user-service", userId,
            erasedEventId, DateTimeOffset.UtcNow, $"{{\"userId\":\"{userId}\",\"erasedAt\":\"{DateTimeOffset.UtcNow:O}\"}}");

        var redactedRow = await WaitForAsync(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            return await db.IngestedEvents.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == domainRegisteredEventId && e.PiiRedactedAt != null);
        }, TimeSpan.FromSeconds(60));

        redactedRow.Payload.Should().NotContain("pii-e2e@example.com");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var redactionRecord = await db.PiiRedactionRecords.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == userId);
        redactionRecord.Should().NotBeNull();
        redactionRecord!.RowsRedacted.Should().BeGreaterThanOrEqualTo(1);
    }
}
