using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using DomainEventId = Kart.Analytics.Domain.ValueObjects.EventId;

namespace Kart.Analytics.Application.Features.RedactUserPii;

public sealed class RedactUserPiiCommandHandler(
    IIngestedEventRepository ingestedEventRepository,
    IPiiRedactionRecordRepository piiRedactionRecordRepository,
    IClock clock,
    ILogger<RedactUserPiiCommandHandler> logger) : IRequestHandler<RedactUserPiiCommand, Unit>
{
    public async Task<Unit> Handle(RedactUserPiiCommand request, CancellationToken cancellationToken)
    {
        var pendingRows = await ingestedEventRepository.GetPiiPendingForUserAsync(request.UserId, cancellationToken);
        var now = clock.UtcNow;

        foreach (var row in pendingRows)
        {
            var redactedPayload = PiiRedactor.Redact(row.Payload);
            row.RedactPii(redactedPayload, now, SystemPrincipals.PiiRedactionSweep);
        }

        if (pendingRows.Count > 0)
        {
            await ingestedEventRepository.SaveRedactedBatchAsync(pendingRows, cancellationToken);
        }

        var record = PiiRedactionRecord.Create(
            request.UserId,
            DomainEventId.From(request.TriggeringEventId),
            pendingRows.Count,
            now,
            SystemPrincipals.PiiRedactionSweep);

        await piiRedactionRecordRepository.AddAsync(record, cancellationToken);

        logger.LogInformation(
            "Stage {Stage}: redacted {RowCount} rows for user {UserId}, triggered by event {TriggeringEventId}",
            "UserDataRedacted",
            pendingRows.Count,
            request.UserId,
            request.TriggeringEventId);

        return Unit.Value;
    }
}
