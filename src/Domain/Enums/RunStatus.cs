namespace Kart.Analytics.Domain.Enums;

/// <summary>
/// database-design.md `analytics_reconciliation_runs.status` CHECK constraint. ddd-model.md
/// invariant: only transitions `Running` → `Completed` or `Running` → `Failed`, never regresses —
/// a failed run is retried as a fresh attempt against the same <see cref="Kart.Analytics.Domain.ValueObjects.RunDate"/> row, not by un-failing this one.
/// </summary>
public enum RunStatus
{
    Running,
    Completed,
    Failed,
}
