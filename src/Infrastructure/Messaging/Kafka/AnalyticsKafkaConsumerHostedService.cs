using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Kart.Analytics.Application.Features.HandleIngestionWriteFailure;
using Kart.Analytics.Application.Features.IngestEvent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kart.Analytics.Infrastructure.Messaging.Kafka;

/// <summary>
/// ANL-1/ANL-2: the platform's full event fan-in lands here. One consumer group, subscribed
/// across every publisher's own topic (<see cref="KafkaOptions.Topics"/>), dispatching every
/// message through the same generic <see cref="IngestEventCommand"/> regardless of which of the
/// ~35 event types it is — keyed by the `eventType` header, never per-event code.
///
/// Mirrors kart-recommendation-service's <c>ClickstreamConsumerHostedService</c> exactly:
/// Confluent.Kafka's blocking <c>Consume</c> has no async overload, so this runs on its own
/// dedicated thread via <see cref="Task.Run(Action, CancellationToken)"/> — the library's own
/// supported pattern, not a shortcut.
///
/// Retry/DLQ/offset-commit semantics (event-contract.md D5, Domain Invariant #4): up to
/// <see cref="KafkaOptions.MaxRetryAttempts"/> in-process retries with backoff, then
/// <see cref="HandleIngestionWriteFailureCommand"/> parks the event in `analytics_dlq_events`.
/// The Kafka offset is committed only after ONE of those two outcomes succeeds — never left
/// uncommitted — so a poison message can never stall this partition or backpressure any upstream
/// publisher.
/// </summary>
public sealed class AnalyticsKafkaConsumerHostedService(
    IOptions<KafkaOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<AnalyticsKafkaConsumerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunConsumeLoop(stoppingToken), stoppingToken);

    private void RunConsumeLoop(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Declarative topic provisioning — the same "config-driven, no manual setup"
                // philosophy the platform already applies to RabbitMQ topology
                // (RabbitMqTopologyProvisioner). Subscribing to a topic that doesn't exist yet
                // makes every subsequent Consume() call throw "Unknown topic or partition"
                // indefinitely on some broker configurations (auto.create.topics.enable is not
                // reliably on, e.g. in test brokers) — since most of this service's 15 topics
                // won't exist until their owning publisher ships its own Kafka dual-publish, this
                // consumer creates whichever of its own subscribed topics are still missing
                // itself, rather than depending on implicit broker-side auto-creation.
                EnsureTopicsExist(opts);

                var consumerConfig = new ConsumerConfig
                {
                    BootstrapServers = opts.BootstrapServers,
                    GroupId = opts.ConsumerGroup,
                    EnableAutoCommit = false,
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                };

                using var consumer = new ConsumerBuilder<string, byte[]>(consumerConfig).Build();
                consumer.Subscribe(opts.Topics);

                logger.LogInformation(
                    "Analytics Kafka consumer subscribed to {TopicCount} topics as group {ConsumerGroup}",
                    opts.Topics.Length,
                    opts.ConsumerGroup);

                while (!stoppingToken.IsCancellationRequested)
                {
                    ConsumeResult<string, byte[]>? result;
                    try
                    {
                        result = consumer.Consume(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ConsumeException ex)
                    {
                        logger.LogError(ex, "Kafka consume error across subscribed topics {Topics}", string.Join(",", opts.Topics));
                        continue;
                    }

                    if (result?.Message is null)
                    {
                        continue;
                    }

                    HandleRecordWithRetry(result, opts, stoppingToken);
                    consumer.Commit(result);
                }

                consumer.Close();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Analytics Kafka consumer lost its connection; reconnecting in {Delay}", ReconnectDelay);
                Thread.Sleep(ReconnectDelay);
            }
        }
    }

    private void EnsureTopicsExist(KafkaOptions opts)
    {
        using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = opts.BootstrapServers }).Build();

        var existingTopics = adminClient.GetMetadata(TimeSpan.FromSeconds(10)).Topics.Select(t => t.Topic).ToHashSet();
        var missingTopics = opts.Topics.Where(t => !existingTopics.Contains(t)).ToList();
        if (missingTopics.Count == 0)
        {
            return;
        }

        try
        {
            adminClient.CreateTopicsAsync(missingTopics.Select(t => new TopicSpecification { Name = t, NumPartitions = 1, ReplicationFactor = 1 }))
                .GetAwaiter().GetResult();
        }
        catch (CreateTopicsException ex)
        {
            // Idempotent: tolerate a race where another consumer instance (or the broker's own
            // auto-creation) created the same topic between the metadata check above and this
            // call — any other failure reason still surfaces.
            var unexpected = ex.Results.Where(r => r.Error.Code != ErrorCode.TopicAlreadyExists).ToList();
            if (unexpected.Count > 0)
            {
                throw;
            }
        }
    }

    private void HandleRecordWithRetry(ConsumeResult<string, byte[]> result, KafkaOptions opts, CancellationToken stoppingToken)
    {
        Exception? lastFailure = null;

        for (var attempt = 1; attempt <= opts.MaxRetryAttempts; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                DispatchIngestAsync(result, scope.ServiceProvider, stoppingToken).GetAwaiter().GetResult();

                logger.LogInformation(
                    "Stage {Stage}: consumed record from {Topic}[{Partition}]@{Offset}",
                    "EventConsumed",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value);
                return;
            }
            catch (Exception ex)
            {
                lastFailure = ex;
                if (attempt < opts.MaxRetryAttempts)
                {
                    logger.LogWarning(ex,
                        "Ingestion attempt {Attempt}/{MaxAttempts} failed for {Topic}[{Partition}]@{Offset}",
                        attempt, opts.MaxRetryAttempts, result.Topic, result.Partition.Value, result.Offset.Value);
                    Thread.Sleep(TimeSpan.FromMilliseconds(200 * attempt));
                }
            }
        }

        // Retry budget exhausted — hand off to the DLQ path. event-contract.md D5 scopes retry
        // to the write-to-analytics_raw_events failure mode only; if THIS call also throws, it
        // surfaces as an unhandled exception here, which — per Domain Invariant #4's own
        // boundary — deliberately leaves the offset uncommitted (the enclosing RunConsumeLoop's
        // catch-and-reconnect handles it) rather than silently dropping the event, so this record
        // is redelivered and the whole attempt sequence retried from scratch on the next poll.
        using var dlqScope = scopeFactory.CreateScope();
        DispatchDeadLetterAsync(result, lastFailure!, opts.MaxRetryAttempts, dlqScope.ServiceProvider, stoppingToken).GetAwaiter().GetResult();

        logger.LogWarning(
            "Stage {Stage}: {Topic}[{Partition}]@{Offset} exhausted {MaxAttempts} attempts; routed to analytics_dlq_events",
            "EventDeadLettered",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            opts.MaxRetryAttempts);
    }

    private static async Task DispatchIngestAsync(ConsumeResult<string, byte[]> result, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var envelope = ParsedEventEnvelope.From(result);
        var sender = serviceProvider.GetRequiredService<ISender>();
        await sender.Send(
            new IngestEventCommand(envelope.EventId, envelope.EventType, envelope.PublisherService, envelope.PartitionKey, envelope.OccurredAt, envelope.PayloadJson),
            cancellationToken);
    }

    private static async Task DispatchDeadLetterAsync(ConsumeResult<string, byte[]> result, Exception failure, int retryCount, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var (eventId, eventType, payloadJson) = TryExtractForDlq(result);
        var sender = serviceProvider.GetRequiredService<ISender>();
        await sender.Send(
            new HandleIngestionWriteFailureCommand(eventId, eventType, payloadJson, failure.Message, retryCount),
            cancellationToken);
    }

    private static (Guid EventId, string EventType, string PayloadJson) TryExtractForDlq(ConsumeResult<string, byte[]> result)
    {
        var payloadJson = Encoding.UTF8.GetString(result.Message.Value);
        try
        {
            var envelope = ParsedEventEnvelope.From(result);
            return (envelope.EventId, envelope.EventType, envelope.PayloadJson);
        }
        catch
        {
            // The message was unparseable even for DLQ metadata extraction (edge-cases.md
            // "Schema Evolution" tolerant-reader fallback) — still park it, with a fresh
            // synthetic event id and "Unknown" type, so the raw bytes are never silently lost.
            return (Guid.NewGuid(), "Unknown", payloadJson);
        }
    }

    private sealed record ParsedEventEnvelope(Guid EventId, string EventType, string PublisherService, string PartitionKey, DateTimeOffset OccurredAt, string PayloadJson)
    {
        public static ParsedEventEnvelope From(ConsumeResult<string, byte[]> result)
        {
            var payloadJson = Encoding.UTF8.GetString(result.Message.Value);
            var headers = result.Message.Headers;

            var eventType = ReadHeaderOrBodyString(headers, payloadJson, "eventType")
                ?? throw new InvalidOperationException("Message carries no 'eventType' header or top-level JSON field.");
            var publisherService = ReadHeaderOrBodyString(headers, payloadJson, "publisherService") ?? "unknown";
            var eventIdRaw = ReadHeaderOrBodyString(headers, payloadJson, "eventId");
            var eventId = eventIdRaw is not null ? Guid.Parse(eventIdRaw) : Guid.NewGuid();
            var occurredAtRaw = ReadHeaderOrBodyString(headers, payloadJson, "occurredAt");
            var occurredAt = occurredAtRaw is not null ? DateTimeOffset.Parse(occurredAtRaw) : DateTimeOffset.UtcNow;
            var partitionKey = result.Message.Key ?? eventId.ToString();

            return new ParsedEventEnvelope(eventId, eventType, publisherService, partitionKey, occurredAt, payloadJson);
        }

        /// <summary>Tolerant reader (edge-cases.md "Schema Evolution"): prefers the Kafka message
        /// header, falls back to a same-named top-level JSON field for producers/tooling that
        /// can't easily set headers (matching kart-recommendation-service's own convention).</summary>
        private static string? ReadHeaderOrBodyString(Headers headers, string payloadJson, string name)
        {
            if (headers.TryGetLastBytes(name, out var headerBytes))
            {
                return Encoding.UTF8.GetString(headerBytes);
            }

            try
            {
                using var document = JsonDocument.Parse(payloadJson);
                return document.RootElement.TryGetProperty(name, out var property) ? property.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
