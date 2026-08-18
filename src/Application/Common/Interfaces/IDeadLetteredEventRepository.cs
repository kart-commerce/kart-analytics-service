using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.Application.Common.Interfaces;

/// <summary>The sole repository for the <see cref="DeadLetteredEvent"/> aggregate root.</summary>
public interface IDeadLetteredEventRepository
{
    Task AddAsync(DeadLetteredEvent deadLetteredEvent, CancellationToken cancellationToken);

    /// <summary>Oldest-first, not-yet-reprocessed rows — backs the scheduled reprocessor (ANL-3),
    /// using `idx_analytics_dlq_events_pending`.</summary>
    Task<IReadOnlyList<DeadLetteredEvent>> GetPendingBatchAsync(int batchSize, CancellationToken cancellationToken);

    Task<DeadLetteredEvent?> FindByIdAsync(DlqId dlqId, CancellationToken cancellationToken);

    Task MarkReprocessedAsync(DeadLetteredEvent deadLetteredEvent, DateTimeOffset reprocessedAt, string updatedBy, CancellationToken cancellationToken);
}
