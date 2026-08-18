namespace Kart.Analytics.Infrastructure.Messaging.Kafka;

/// <summary>
/// design-decisions.md's "Ingestion Transport &amp; Communication Style" decision: Kafka,
/// consumer-only, no RabbitMQ exchange owned by this service at all (confirmed by this service's
/// own `message-bus-manifest.json`, `"transport": "kafka-only"`). Analytics is a full fan-in
/// consumer (ADR-0004) across every one of the platform's 15 publishing services, so — unlike
/// kart-recommendation-service's single clickstream topic — this subscribes to the union of every
/// publisher's own topic, named `kart.&lt;service&gt;.events` per kart-conventions.md's
/// `kart.&lt;service&gt;.&lt;entity&gt;` topic-naming example (one topic per publisher covers that
/// publisher's own several event types, dispatched by the `eventType` message header — ANL-1's
/// "single generic handler," not per-event-type topics).
/// </summary>
public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>Every publishing service's own topic, per event-contract.md's full fan-in list —
    /// the default here is this build's own topic-naming choice (no publisher has dual-published
    /// to Kafka yet on this platform), overridable via config if a real topology differs.</summary>
    public static readonly string[] DefaultTopics =
    [
        "kart.order.events",
        "kart.inventory.events",
        "kart.payment.events",
        "kart.shipping.events",
        "kart.delivery-tracking.events",
        "kart.product.events",
        "kart.review.events",
        "kart.category.events",
        "kart.offer.events",
        "kart.user.events",
        "kart.identity.events",
        "kart.notification.events",
        "kart.cart.events",
        "kart.wishlist.events",
        "kart.admin.events",
    ];

    public string BootstrapServers { get; init; } = "localhost:9092";

    public string[] Topics { get; init; } = DefaultTopics;

    public string ConsumerGroup { get; init; } = "kart-analytics-service";

    /// <summary>event-contract.md D5: "3x exponential backoff" before parking in `analytics_dlq_events`.</summary>
    public int MaxRetryAttempts { get; init; } = 3;
}
