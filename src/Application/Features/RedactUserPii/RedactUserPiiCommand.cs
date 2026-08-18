using MediatR;

namespace Kart.Analytics.Application.Features.RedactUserPii;

/// <summary>
/// ANL-4: redact-in-place sweep triggered by consuming `UserDataErased` for a given user
/// (ADR-0016 item 6) — database-design.md "PII Redaction on UserDataErased": never hard-delete,
/// never merely tag-for-exclusion.
/// </summary>
public sealed record RedactUserPiiCommand(string UserId, Guid TriggeringEventId) : IRequest<Unit>;
