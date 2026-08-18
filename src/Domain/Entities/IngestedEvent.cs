using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.Domain.Entities;

/// <summary>
/// Aggregate root — database-design.md `analytics_raw_events`. The single source of truth every
/// dashboard/funnel read model is recomputed from (design-decisions.md "Idempotency Mechanism for
/// Replay-Safe Aggregation"). One row per distinct <see cref="EventId"/>, idempotently upserted:
/// a redelivered or replayed event calls <see cref="ReplaceOnReplay"/> to overwrite this same row
/// rather than a new row being inserted (edge-cases.md "Replay Correctness") — this is what lets
/// live ingestion and replay share one code path safely without double-counting.
/// </summary>
public sealed class IngestedEvent
{
    public EventId EventId { get; private set; }
    public EventEnvelope Envelope { get; private set; } = null!;
    public SchemaVersionPointer SchemaVersion { get; private set; } = null!;

    /// <summary>The event's own schema-validated body, opaque JSON. Never decomposed into typed
    /// per-domain reference fields (e.g. no modeled `OrderId`) — ddd-model.md Modeling Decision 3:
    /// this opacity IS this service's Anti-Corruption Layer.</summary>
    public string Payload { get; private set; } = string.Empty;

    public bool ContainsPii { get; private set; }
    public DateTimeOffset IngestedAt { get; private set; }

    /// <summary>Null until a redaction sweep (see <see cref="RedactPii"/>) has touched this row in
    /// response to a `UserDataErased` event for the same user; never set for rows that never
    /// carried PII in the first place.</summary>
    public DateTimeOffset? PiiRedactedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private IngestedEvent()
    {
    }

    public static IngestedEvent Create(
        EventId eventId,
        EventEnvelope envelope,
        SchemaVersionPointer schemaVersion,
        string payload,
        bool containsPii,
        DateTimeOffset now,
        string createdBy) =>
        new()
        {
            EventId = eventId,
            Envelope = envelope,
            SchemaVersion = schemaVersion,
            Payload = payload,
            ContainsPii = containsPii,
            IngestedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
        };

    /// <summary>Overwrites this row's content on a redelivered/replayed message with the same
    /// <see cref="EventId"/> — the idempotent-upsert path (edge-cases.md "Replay Correctness").
    /// <see cref="IngestedAt"/> (the first-landing time) is deliberately NOT touched; only
    /// <see cref="UpdatedAt"/>/<see cref="UpdatedBy"/> move.</summary>
    public void ReplaceOnReplay(
        EventEnvelope envelope,
        SchemaVersionPointer schemaVersion,
        string payload,
        bool containsPii,
        DateTimeOffset now,
        string updatedBy)
    {
        Envelope = envelope;
        SchemaVersion = schemaVersion;
        Payload = payload;
        ContainsPii = containsPii;
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// database-design.md "PII Redaction on UserDataErased": redact in place, never hard-delete or
    /// merely tag-for-exclusion — preserves the non-PII facts (order totals, timestamps, event
    /// type) so every dashboard/funnel aggregate stays stable across a raw-storage replay, while
    /// still satisfying GDPR erasure for the actual personal data.
    /// </summary>
    public void RedactPii(string redactedPayload, DateTimeOffset redactedAt, string updatedBy)
    {
        if (!ContainsPii)
        {
            throw new InvalidOperationException($"Event {EventId} does not carry PII and cannot be redacted.");
        }

        Payload = redactedPayload;
        PiiRedactedAt = redactedAt;
        UpdatedAt = redactedAt;
        UpdatedBy = updatedBy;
    }
}
