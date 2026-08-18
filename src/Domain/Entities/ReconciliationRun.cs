using Kart.Analytics.Domain.Enums;
using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.Domain.Entities;

/// <summary>
/// Aggregate root — database-design.md `analytics_reconciliation_runs`. Bookkeeping for the
/// nightly batch reconciliation run (edge-cases.md "Out-of-Order Event Arrival"; architecture.md's
/// proposed 06:00 UTC completion target). At most one run per <see cref="RunDate"/>
/// (`UNIQUE(run_date)`); its completion is what flips every read-model doc's `isProvisional` to
/// `false` and sets `reconciledThrough`.
/// </summary>
public sealed class ReconciliationRun
{
    public RunId RunId { get; private set; }
    public RunDate RunDate { get; private set; }
    public RunStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private ReconciliationRun()
    {
    }

    public static ReconciliationRun StartNew(RunDate runDate, DateTimeOffset now, string createdBy) =>
        new()
        {
            RunId = RunId.New(),
            RunDate = runDate,
            Status = RunStatus.Running,
            StartedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
        };

    /// <summary>
    /// Status transitions Running → Completed or Running → Failed, and Failed → Running (a retried
    /// attempt reuses this same row rather than inserting a second one, since `run_date` is
    /// database-unique — one row per calendar date is a hard schema invariant, not just a
    /// convention). Once <see cref="RunStatus.Completed"/>, this row is final — a completed run's
    /// completion is never undone, which is the actual "never regresses" guarantee (ddd-model.md).
    /// </summary>
    public void Complete(DateTimeOffset completedAt, string updatedBy)
    {
        EnsureRunning();
        Status = RunStatus.Completed;
        CompletedAt = completedAt;
        UpdatedAt = completedAt;
        UpdatedBy = updatedBy;
    }

    public void Fail(DateTimeOffset failedAt, string updatedBy)
    {
        EnsureRunning();
        Status = RunStatus.Failed;
        UpdatedAt = failedAt;
        UpdatedBy = updatedBy;
    }

    /// <summary>Re-attempts a previously failed run against the same <see cref="RunDate"/> row.</summary>
    public void Retry(DateTimeOffset retriedAt, string updatedBy)
    {
        if (Status != RunStatus.Failed)
        {
            throw new InvalidOperationException($"Reconciliation run {RunId} for {RunDate} is {Status}, not Failed — cannot retry.");
        }

        Status = RunStatus.Running;
        StartedAt = retriedAt;
        CompletedAt = null;
        UpdatedAt = retriedAt;
        UpdatedBy = updatedBy;
    }

    private void EnsureRunning()
    {
        if (Status != RunStatus.Running)
        {
            throw new InvalidOperationException($"Reconciliation run {RunId} for {RunDate} is already {Status} and cannot transition again.");
        }
    }
}
