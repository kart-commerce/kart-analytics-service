using FluentAssertions;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.UnitTests.Domain;

public class IngestedEventTests
{
    private static EventEnvelope Envelope(DateTimeOffset occurredAt) => EventEnvelope.Create("OrderCreated", "kart-order-service", "order-1", occurredAt);
    private static SchemaVersionPointer SchemaVersion() => SchemaVersionPointer.Create("abc123", "1.0");

    [Fact]
    public void ReplaceOnReplay_does_not_change_first_landing_time()
    {
        var firstLanded = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var replayedAt = DateTimeOffset.Parse("2026-08-02T00:00:00Z");
        var ingestedEvent = IngestedEvent.Create(EventId.New(), Envelope(firstLanded), SchemaVersion(), "{}", false, firstLanded, "system:analytics-ingestion-consumer");

        ingestedEvent.ReplaceOnReplay(Envelope(firstLanded), SchemaVersion(), "{\"total\":5}", false, replayedAt, "system:analytics-ingestion-consumer");

        ingestedEvent.IngestedAt.Should().Be(firstLanded);
        ingestedEvent.UpdatedAt.Should().Be(replayedAt);
        ingestedEvent.Payload.Should().Be("{\"total\":5}");
    }

    [Fact]
    public void RedactPii_throws_when_event_never_carried_pii()
    {
        var now = DateTimeOffset.UtcNow;
        var ingestedEvent = IngestedEvent.Create(EventId.New(), Envelope(now), SchemaVersion(), "{}", containsPii: false, now, "system:analytics-ingestion-consumer");

        var act = () => ingestedEvent.RedactPii("{}", now, "system:analytics-pii-redaction-sweep");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RedactPii_sets_redacted_at_and_updates_payload()
    {
        var now = DateTimeOffset.UtcNow;
        var ingestedEvent = IngestedEvent.Create(EventId.New(), Envelope(now), SchemaVersion(), "{\"email\":\"a@b.com\"}", containsPii: true, now, "system:analytics-ingestion-consumer");

        var redactedAt = now.AddMinutes(5);
        ingestedEvent.RedactPii("{\"email\":\"[redacted]\"}", redactedAt, "system:analytics-pii-redaction-sweep");

        ingestedEvent.PiiRedactedAt.Should().Be(redactedAt);
        ingestedEvent.Payload.Should().Be("{\"email\":\"[redacted]\"}");
        ingestedEvent.UpdatedBy.Should().Be("system:analytics-pii-redaction-sweep");
    }
}
