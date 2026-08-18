using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.Domain.Entities;

/// <summary>
/// Aggregate root — database-design.md `analytics_pii_redactions`. Immutable once written
/// (ddd-model.md invariant): a later redaction sweep for the same user creates a NEW record rather
/// than mutating this one. The compliance-facing audit trail of "this user's data was redacted,
/// when, and how many rows" (ADR-0016 item 6) — independent of any single `analytics_raw_events`
/// row, since one sweep typically touches many.
/// </summary>
public sealed class PiiRedactionRecord
{
    public RedactionId RedactionId { get; private set; }
    public string UserId { get; private set; } = string.Empty;

    /// <summary>The `UserDataErased` event id that triggered this sweep.</summary>
    public EventId TriggeringEventId { get; private set; }

    public int RowsRedacted { get; private set; }
    public DateTimeOffset RedactedAt { get; private set; }

    /// <summary>No `UpdatedBy`/`UpdatedAt` — this row is immutable once written, so there is no
    /// "most recent update" to attribute (database-design.md).</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    private PiiRedactionRecord()
    {
    }

    public static PiiRedactionRecord Create(
        string userId,
        EventId triggeringEventId,
        int rowsRedacted,
        DateTimeOffset redactedAt,
        string createdBy)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id must not be empty.", nameof(userId));
        }

        return new PiiRedactionRecord
        {
            RedactionId = RedactionId.New(),
            UserId = userId,
            TriggeringEventId = triggeringEventId,
            RowsRedacted = rowsRedacted,
            RedactedAt = redactedAt,
            CreatedBy = createdBy,
        };
    }
}
