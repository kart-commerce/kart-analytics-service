namespace Kart.Analytics.Domain.ValueObjects;

/// <summary>
/// Common metadata every one of the ~35 platform events Analytics consumes carries
/// (ddd-model.md) — deliberately the only structured thing this service ever extracts from an
/// event's own payload. <see cref="EventType"/> is intentionally a plain, unvalidated-against-any-
/// allowlist string, not a closed enum: ADR-0004's full-fan-in design means Analytics accepts any
/// event type by construction (event-contract.md, tickets.md ANL-1 — "single generic handler keyed
/// by event_type/schema, not per-event code") — a closed set here would contradict that design and
/// require a code change every time any of the platform's other 17 services adds a new event.
/// </summary>
/// <param name="EventType">e.g. "OrderCreated", "PaymentCompleted" — no allowlist, full fan-in per ADR-0004.</param>
/// <param name="PublisherService">e.g. "kart-order-service" — informational, for the completeness audits architecture.md runs.</param>
/// <param name="PartitionKey">The aggregate/entity id used as the Kafka partition key — preserves per-entity ordering (design-decisions.md "Concurrency/Scaling" decision).</param>
/// <param name="OccurredAt">Event-time (the publisher's own timestamp) — the basis for event-time windowing/watermarks (edge-cases.md "Out-of-Order Event Arrival").</param>
public sealed record EventEnvelope(string EventType, string PublisherService, string PartitionKey, DateTimeOffset OccurredAt)
{
    public static EventEnvelope Create(string eventType, string publisherService, string partitionKey, DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type must not be empty.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(publisherService))
        {
            throw new ArgumentException("Publisher service must not be empty.", nameof(publisherService));
        }

        if (string.IsNullOrWhiteSpace(partitionKey))
        {
            throw new ArgumentException("Partition key must not be empty.", nameof(partitionKey));
        }

        return new EventEnvelope(eventType, publisherService, partitionKey, occurredAt);
    }
}
