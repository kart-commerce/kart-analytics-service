using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.Enums;
using Kart.Analytics.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kart.Analytics.Infrastructure.Persistence.Repositories;

/// <summary>The sole repository for the <see cref="ReconciliationRun"/> aggregate root.</summary>
public sealed class ReconciliationRunRepository(AnalyticsDbContext dbContext) : IReconciliationRunRepository
{
    public async Task<ReconciliationRun?> FindByRunDateAsync(RunDate runDate, CancellationToken cancellationToken) =>
        await dbContext.ReconciliationRuns.SingleOrDefaultAsync(r => r.RunDate == runDate, cancellationToken);

    public async Task AddAsync(ReconciliationRun run, CancellationToken cancellationToken)
    {
        dbContext.ReconciliationRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(ReconciliationRun run, CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);

    public async Task<ReconciliationRun?> GetLastCompletedAsync(CancellationToken cancellationToken) =>
        await dbContext.ReconciliationRuns
            .Where(r => r.Status == RunStatus.Completed)
            .OrderByDescending(r => r.RunDate)
            .FirstOrDefaultAsync(cancellationToken);
}
