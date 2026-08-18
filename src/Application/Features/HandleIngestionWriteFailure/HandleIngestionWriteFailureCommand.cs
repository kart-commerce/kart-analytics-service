using MediatR;

namespace Kart.Analytics.Application.Features.HandleIngestionWriteFailure;

/// <summary>
/// ANL-2: parks an event whose ingestion write (ANL-1) exhausted its 3x exponential-backoff retry
/// budget — design-decisions.md "Resilience Pattern". Dispatched by the Kafka consumer hosted
/// service (Infrastructure), never by <c>IngestEventCommandHandler</c> itself: retry orchestration
/// lives in the consumer loop, matching kart-recommendation-service's
/// <c>ClickstreamConsumerHostedService</c> precedent exactly.
/// </summary>
/// <param name="EventId">The event id that failed to write — referenced by value, never a foreign key (ddd-model.md).</param>
/// <param name="EventType">Preserved for DLQ triage even though the write to the typed store never succeeded.</param>
/// <param name="PayloadJson">The raw payload, unmodified, so the reprocessor (ANL-3) can replay it verbatim.</param>
/// <param name="FailureReason">The last exception's message from the exhausted retry attempts.</param>
/// <param name="RetryCount">Always 3 at hand-off time per D5, passed explicitly rather than hardcoded in case the tier is revisited.</param>
public sealed record HandleIngestionWriteFailureCommand(
    Guid EventId,
    string EventType,
    string PayloadJson,
    string FailureReason,
    int RetryCount) : IRequest<Unit>;
