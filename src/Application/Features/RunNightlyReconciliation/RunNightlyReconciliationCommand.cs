using MediatR;

namespace Kart.Analytics.Application.Features.RunNightlyReconciliation;

/// <summary>
/// ANL-5: the nightly batch reconciler half of the CQRS sync mechanism — fully recomputes every
/// dashboard/funnel bucket touching <paramref name="TargetDate"/> from `analytics_raw_events`,
/// across every granularity, then flips those buckets to `isProvisional:false`. At most one
/// completed run per `RunDate` (database-design.md `UNIQUE(run_date)`).
/// </summary>
/// <param name="TargetDate">Defaults to "yesterday" (UTC) when null — the calendar day being finalized.</param>
public sealed record RunNightlyReconciliationCommand(DateOnly? TargetDate) : IRequest<RunNightlyReconciliationResult>;

public enum ReconciliationOutcome
{
    Completed,
    AlreadyCompleted,
    SkippedAlreadyRunning,
    Failed,
}

public sealed record RunNightlyReconciliationResult(ReconciliationOutcome Outcome, DateOnly TargetDate);
