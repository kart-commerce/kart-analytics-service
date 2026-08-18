using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kart.Analytics.Infrastructure.Persistence.Repositories;

/// <summary>The sole repository for the <see cref="DeadLetteredEvent"/> aggregate root.</summary>
public sealed class DeadLetteredEventRepository(AnalyticsDbContext dbContext) : IDeadLetteredEventRepository
{
    public async Task AddAsync(DeadLetteredEvent deadLetteredEvent, CancellationToken cancellationToken)
    {
        dbContext.DeadLetteredEvents.Add(deadLetteredEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeadLetteredEvent>> GetPendingBatchAsync(int batchSize, CancellationToken cancellationToken) =>
        await dbContext.DeadLetteredEvents
            .Where(e => e.ReprocessedAt == null)
            .OrderBy(e => e.DlqLandedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<DeadLetteredEvent?> FindByIdAsync(DlqId dlqId, CancellationToken cancellationToken) =>
        await dbContext.DeadLetteredEvents.SingleOrDefaultAsync(e => e.DlqId == dlqId, cancellationToken);

    public async Task MarkReprocessedAsync(DeadLetteredEvent deadLetteredEvent, DateTimeOffset reprocessedAt, string updatedBy, CancellationToken cancellationToken)
    {
        deadLetteredEvent.MarkReprocessed(reprocessedAt, updatedBy);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
