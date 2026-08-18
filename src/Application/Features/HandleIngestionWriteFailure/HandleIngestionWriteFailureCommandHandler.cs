using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using DomainEventId = Kart.Analytics.Domain.ValueObjects.EventId;

namespace Kart.Analytics.Application.Features.HandleIngestionWriteFailure;

public sealed class HandleIngestionWriteFailureCommandHandler(
    IDeadLetteredEventRepository repository,
    IClock clock,
    ILogger<HandleIngestionWriteFailureCommandHandler> logger) : IRequestHandler<HandleIngestionWriteFailureCommand, Unit>
{
    public async Task<Unit> Handle(HandleIngestionWriteFailureCommand request, CancellationToken cancellationToken)
    {
        var deadLetteredEvent = DeadLetteredEvent.Create(
            DomainEventId.From(request.EventId),
            request.EventType,
            request.PayloadJson,
            request.FailureReason,
            request.RetryCount,
            clock.UtcNow,
            SystemPrincipals.IngestionConsumer);

        await repository.AddAsync(deadLetteredEvent, cancellationToken);

        logger.LogWarning(
            "Stage {Stage}: event {EventId} of type {EventType} exhausted {RetryCount} retries; parked as {DlqId} — {FailureReason}",
            "EventDeadLettered",
            request.EventId,
            request.EventType,
            request.RetryCount,
            deadLetteredEvent.DlqId,
            request.FailureReason);

        return Unit.Value;
    }
}
