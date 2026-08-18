using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.Application.Common.Interfaces;

/// <summary>The sole repository for the <see cref="ReconciliationRun"/> aggregate root.</summary>
public interface IReconciliationRunRepository
{
    /// <summary>Backs the "has today's run already started/completed" check before kicking off
    /// the nightly job — prevents a double-run for the same `RunDate`
    /// (database-design.md `UNIQUE(run_date)`).</summary>
    Task<ReconciliationRun?> FindByRunDateAsync(RunDate runDate, CancellationToken cancellationToken);

    Task AddAsync(ReconciliationRun run, CancellationToken cancellationToken);

    Task SaveAsync(ReconciliationRun run, CancellationToken cancellationToken);

    /// <summary>The most recently completed run — its `RunDate` is the `reconciledThrough` value
    /// every dashboard response's envelope reports.</summary>
    Task<ReconciliationRun?> GetLastCompletedAsync(CancellationToken cancellationToken);
}
