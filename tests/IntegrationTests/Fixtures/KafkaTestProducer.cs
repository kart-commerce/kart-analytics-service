using System.Text;
using Confluent.Kafka;

namespace Kart.Analytics.IntegrationTests.Fixtures;

/// <summary>Produces a single test event onto a real Kafka topic, using the same header
/// convention (`eventType`/`eventId`/`publisherService`/`occurredAt`) the real consumer
/// (`AnalyticsKafkaConsumerHostedService`) reads — so these tests exercise the actual wire
/// format, not a shortcut.</summary>
public static class KafkaTestProducer
{
    public static async Task ProduceAsync(
        string bootstrapServers,
        string topic,
        string eventType,
        string publisherService,
        string partitionKey,
        Guid eventId,
        DateTimeOffset occurredAt,
        string payloadJson)
    {
        using var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = bootstrapServers }).Build();

        var message = new Message<string, byte[]>
        {
            Key = partitionKey,
            Value = Encoding.UTF8.GetBytes(payloadJson),
            Headers = new Headers
            {
                { "eventType", Encoding.UTF8.GetBytes(eventType) },
                { "eventId", Encoding.UTF8.GetBytes(eventId.ToString()) },
                { "publisherService", Encoding.UTF8.GetBytes(publisherService) },
                { "occurredAt", Encoding.UTF8.GetBytes(occurredAt.ToString("O")) },
            },
        };

        await producer.ProduceAsync(topic, message);
        producer.Flush(TimeSpan.FromSeconds(5));
    }

    /// <summary>Produces a deliberately malformed message (not valid JSON) — exercises the
    /// tolerant-reader/DLQ path.</summary>
    public static async Task ProduceMalformedAsync(string bootstrapServers, string topic)
    {
        using var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = bootstrapServers }).Build();
        var message = new Message<string, byte[]> { Key = "malformed", Value = Encoding.UTF8.GetBytes("{not-valid-json") };
        await producer.ProduceAsync(topic, message);
        producer.Flush(TimeSpan.FromSeconds(5));
    }
}
