using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Features.IngestEvent;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Analytics.Application.Features.ReprocessDlqEvents;

/// <summary>
/// `analytics_dlq_events` (database-design.md) does not persist `publisher_service`/
/// `partition_key`/`occurred_at` — only `event_id`/`event_type`/`payload` survive the original
/// hand-off. This reprocessor reconstructs the envelope fields from the payload's own top-level
/// JSON (the same tolerant-reader convention the Kafka consumer already uses), falling back to
/// safe defaults when a field genuinely isn't present — an accepted degradation for a path that
/// only runs after ingestion already failed once.
/// </summary>
public sealed class ReprocessDlqEventsCommandHandler(
    IDeadLetteredEventRepository dlqRepository,
    ISender sender,
    IClock clock,
    ILogger<ReprocessDlqEventsCommandHandler> logger) : IRequestHandler<ReprocessDlqEventsCommand, ReprocessDlqEventsResult>
{
    public async Task<ReprocessDlqEventsResult> Handle(ReprocessDlqEventsCommand request, CancellationToken cancellationToken)
    {
        var pending = await dlqRepository.GetPendingBatchAsync(request.BatchSize, cancellationToken);

        var reprocessed = 0;
        var stillFailing = 0;

        foreach (var dlqEvent in pending)
        {
            try
            {
                var payload = new PayloadReader(dlqEvent.Payload);
                var publisherService = payload.GetString("publisherService") ?? "unknown";
                var partitionKey = payload.GetString("partitionKey") ?? dlqEvent.EventId.ToString();
                var occurredAtRaw = payload.GetString("occurredAt");
                var occurredAt = occurredAtRaw is not null && DateTimeOffset.TryParse(occurredAtRaw, out var parsed)
                    ? parsed
                    : dlqEvent.DlqLandedAt;

                await sender.Send(
                    new IngestEventCommand(dlqEvent.EventId.Value, dlqEvent.EventType, publisherService, partitionKey, occurredAt, dlqEvent.Payload),
                    cancellationToken);

                await dlqRepository.MarkReprocessedAsync(dlqEvent, clock.UtcNow, SystemPrincipals.DlqReprocessor, cancellationToken);
                reprocessed++;

                logger.LogInformation(
                    "Stage {Stage}: reprocessed DLQ event {DlqId} (original event {EventId})",
                    "EventReprocessed",
                    dlqEvent.DlqId,
                    dlqEvent.EventId);
            }
            catch (Exception ex)
            {
                stillFailing++;
                logger.LogWarning(ex, "DLQ event {DlqId} (original event {EventId}) still failing on reprocess attempt", dlqEvent.DlqId, dlqEvent.EventId);
            }
        }

        return new ReprocessDlqEventsResult(reprocessed, stillFailing);
    }
}
