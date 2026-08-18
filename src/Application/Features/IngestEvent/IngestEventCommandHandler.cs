using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.SchemaVersioning;
using Kart.Analytics.Application.Features.RedactUserPii;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using DomainEventId = Kart.Analytics.Domain.ValueObjects.EventId;

namespace Kart.Analytics.Application.Features.IngestEvent;

/// <summary>
/// ANL-1's single generic ingestion handler. Idempotency comes entirely from
/// <see cref="IIngestedEventRepository.UpsertAsync"/>'s atomic database-level upsert — this
/// handler does not itself decide fresh-insert-vs-replay (a find-then-branch here would be a race
/// window under concurrent redelivery); it only builds the domain entity and reports back which
/// branch the atomic upsert took, for checkpoint logging.
///
/// The one internal side-effect hook: successfully ingesting a `UserDataErased` event (ANL-4)
/// immediately triggers the redaction sweep for that user — the same "consume an event, react
/// internally" pattern the generic pipeline already uses for every other event, rather than a
/// separate consumer needing to re-read this same event later.
/// </summary>
public sealed class IngestEventCommandHandler(
    IIngestedEventRepository repository,
    IClock clock,
    ISender sender,
    ILogger<IngestEventCommandHandler> logger) : IRequestHandler<IngestEventCommand, IngestEventResult>
{
    private const string UserDataErasedEventType = "UserDataErased";

    public async Task<IngestEventResult> Handle(IngestEventCommand request, CancellationToken cancellationToken)
    {
        var eventId = DomainEventId.From(request.EventId);
        var envelope = EventEnvelope.Create(request.EventType, request.PublisherService, request.PartitionKey, request.OccurredAt);
        var schemaVersion = SchemaVersionResolver.Resolve(request.PayloadJson);
        var containsPii = PiiEventClassification.ContainsPii(request.EventType);
        var now = clock.UtcNow;

        var ingestedEvent = IngestedEvent.Create(eventId, envelope, schemaVersion, request.PayloadJson, containsPii, now, SystemPrincipals.IngestionConsumer);

        var wasFreshInsert = await repository.UpsertAsync(ingestedEvent, cancellationToken);

        logger.LogInformation(
            "Stage {Stage}: event {EventId} of type {EventType} from {PublisherService}",
            wasFreshInsert ? "EventUpsertedFreshInsert" : "EventUpsertedReplayOverwrite",
            eventId,
            request.EventType,
            request.PublisherService);

        logger.LogInformation(
            "Stage {Stage}: event {EventId} persisted to analytics_raw_events",
            "EventIngested",
            eventId);

        if (request.EventType == UserDataErasedEventType)
        {
            var userId = new PayloadReader(request.PayloadJson).GetString("userId");
            if (userId is not null)
            {
                await sender.Send(new RedactUserPiiCommand(userId, request.EventId), cancellationToken);
            }
        }

        return new IngestEventResult(wasFreshInsert);
    }
}
