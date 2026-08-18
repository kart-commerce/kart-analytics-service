using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.Application.Common.Interfaces;

/// <summary>
/// The sole repository for the <see cref="IngestedEvent"/> aggregate root — coding-standards.md's
/// "one repository per Aggregate Root only, never generic IRepository&lt;T&gt;" rule.
/// </summary>
public interface IIngestedEventRepository
{
    Task<IngestedEvent?> FindByIdAsync(EventId eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Idempotent upsert keyed by <see cref="EventId"/> — a redelivered or replayed event
    /// overwrites its own row rather than inserting a duplicate (edge-cases.md "Replay
    /// Correctness"). Implemented as an atomic database-level upsert (not a
    /// find-then-add-or-update round trip), so two concurrent deliveries of the same event id can
    /// never race into a duplicate-key failure or a lost update. If the existing row was already
    /// PII-redacted, the redacted state and payload are preserved rather than being overwritten by
    /// a replay's un-redacted payload — a replay must never undo a compliance redaction.
    /// Returns <c>true</c> if this call performed a fresh insert, <c>false</c> if it overwrote an
    /// existing row (the branch checkpoint-logging distinguishes as
    /// `EventUpsertedFreshInsert`/`EventUpsertedReplayOverwrite`).
    /// </summary>
    Task<bool> UpsertAsync(IngestedEvent ingestedEvent, CancellationToken cancellationToken);

    /// <summary>Every not-yet-redacted PII-bearing row for a given user — backs the redaction
    /// sweep (ANL-4), using `idx_analytics_raw_events_pii_pending`.</summary>
    Task<IReadOnlyList<IngestedEvent>> GetPiiPendingForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Persists a batch of entities already mutated via <see cref="IngestedEvent.RedactPii"/> in one commit.</summary>
    Task SaveRedactedBatchAsync(IReadOnlyList<IngestedEvent> redactedEvents, CancellationToken cancellationToken);

    /// <summary>Every event of the given type, in event-time order, at or after <paramref name="since"/> —
    /// the read pattern both the incremental projector and the nightly reconciler run for every
    /// dashboard/funnel, using `idx_analytics_raw_events_type_occurred`.</summary>
    Task<IReadOnlyList<IngestedEvent>> GetByTypeSinceAsync(string eventType, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Every event of the given type within an inclusive/exclusive event-time window —
    /// backs bucketed dashboard/funnel recompute (nightly reconciler and dashboard-window queries).</summary>
    Task<IReadOnlyList<IngestedEvent>> GetByTypeInWindowAsync(string eventType, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
