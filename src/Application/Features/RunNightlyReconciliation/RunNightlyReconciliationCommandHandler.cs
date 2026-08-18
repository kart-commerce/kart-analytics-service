using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.Enums;
using Kart.Analytics.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Analytics.Application.Features.RunNightlyReconciliation;

/// <summary>
/// Iterates every registered <see cref="IReadModelProjector"/> (one per D4a dashboard/funnel)
/// across all four granularities for the target date's full enclosing bucket window — adding an
/// 11th dashboard later means only registering another projector, this handler's own code never
/// changes (OCP).
/// </summary>
public sealed class RunNightlyReconciliationCommandHandler(
    IReconciliationRunRepository runRepository,
    IEnumerable<IReadModelProjector> projectors,
    IClock clock,
    ILogger<RunNightlyReconciliationCommandHandler> logger) : IRequestHandler<RunNightlyReconciliationCommand, RunNightlyReconciliationResult>
{
    private static readonly Granularity[] AllGranularities = [Granularity.Hour, Granularity.Day, Granularity.Week, Granularity.Month];

    public async Task<RunNightlyReconciliationResult> Handle(RunNightlyReconciliationCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var targetDate = request.TargetDate ?? DateOnly.FromDateTime(now.UtcDateTime.AddDays(-1));
        var runDate = RunDate.From(targetDate);

        var existing = await runRepository.FindByRunDateAsync(runDate, cancellationToken);

        ReconciliationRun run;
        if (existing is null)
        {
            run = ReconciliationRun.StartNew(runDate, now, SystemPrincipals.ReconciliationJob);
            await runRepository.AddAsync(run, cancellationToken);
        }
        else if (existing.Status == RunStatus.Completed)
        {
            return new RunNightlyReconciliationResult(ReconciliationOutcome.AlreadyCompleted, targetDate);
        }
        else if (existing.Status == RunStatus.Running)
        {
            // Another run for this exact date is already in flight (e.g. a concurrent manual
            // trigger) — never start a second one; UNIQUE(run_date) plus this check together
            // prevent a double-write of the same day's reconciled buckets.
            return new RunNightlyReconciliationResult(ReconciliationOutcome.SkippedAlreadyRunning, targetDate);
        }
        else
        {
            run = existing;
            run.Retry(now, SystemPrincipals.ReconciliationJob);
            await runRepository.SaveAsync(run, cancellationToken);
        }

        try
        {
            foreach (var granularity in AllGranularities)
            {
                var (windowFrom, windowTo) = BucketCalculator.GetBucketWindow(targetDate, granularity);
                foreach (var projector in projectors)
                {
                    await projector.RecomputeAsync(windowFrom, windowTo, granularity, isProvisional: false, reconciledThrough: targetDate, cancellationToken);
                }
            }

            run.Complete(clock.UtcNow, SystemPrincipals.ReconciliationJob);
            await runRepository.SaveAsync(run, cancellationToken);

            logger.LogInformation("Stage {Stage}: reconciliation for {TargetDate} completed", "ReconciliationCompleted", targetDate);
            return new RunNightlyReconciliationResult(ReconciliationOutcome.Completed, targetDate);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reconciliation for {TargetDate} failed", targetDate);
            run.Fail(clock.UtcNow, SystemPrincipals.ReconciliationJob);
            await runRepository.SaveAsync(run, cancellationToken);
            return new RunNightlyReconciliationResult(ReconciliationOutcome.Failed, targetDate);
        }
    }
}
