using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.Domain.Entities;

/// <summary>
/// Aggregate root — database-design.md `analytics_dlq_events`. Created only after the 3x
/// exponential-backoff retry budget for a write to <see cref="IngestedEvent"/> is exhausted
/// (design-decisions.md "Resilience Pattern"). References the event that failed to write by value
/// (<see cref="EventId"/>), never by foreign key — deliberate, since the happy path never writes
/// both an <see cref="IngestedEvent"/> and a <see cref="DeadLetteredEvent"/> for the same event
/// (ddd-model.md's mutually-exclusive-outcomes cross-aggregate note).
/// </summary>
public sealed class DeadLetteredEvent
{
    public DlqId DlqId { get; private set; }
    public EventId EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string FailureReason { get; private set; } = string.Empty;
    public int RetryCount { get; private set; }
    public DateTimeOffset DlqLandedAt { get; private set; }

    /// <summary>Set once the scheduled reprocessor (ANL-3) successfully replays this event back
    /// into <see cref="IngestedEvent"/>. Null while still parked/pending.</summary>
    public DateTimeOffset? ReprocessedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    private DeadLetteredEvent()
    {
    }

    public static DeadLetteredEvent Create(
        EventId eventId,
        string eventType,
        string payload,
        string failureReason,
        int retryCount,
        DateTimeOffset dlqLandedAt,
        string createdBy) =>
        new()
        {
            DlqId = DlqId.New(),
            EventId = eventId,
            EventType = eventType,
            Payload = payload,
            FailureReason = failureReason,
            RetryCount = retryCount,
            DlqLandedAt = dlqLandedAt,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
        };

    public void MarkReprocessed(DateTimeOffset reprocessedAt, string updatedBy)
    {
        if (ReprocessedAt is not null)
        {
            throw new InvalidOperationException($"DLQ event {DlqId} was already reprocessed at {ReprocessedAt}.");
        }

        ReprocessedAt = reprocessedAt;
        UpdatedBy = updatedBy;
    }
}
