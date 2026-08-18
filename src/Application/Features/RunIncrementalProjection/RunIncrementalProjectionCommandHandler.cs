using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Analytics.Application.Features.RunIncrementalProjection;

public sealed class RunIncrementalProjectionCommandHandler(
    IEnumerable<IReadModelProjector> projectors,
    IReconciliationRunRepository runRepository,
    IClock clock,
    ILogger<RunIncrementalProjectionCommandHandler> logger) : IRequestHandler<RunIncrementalProjectionCommand, Unit>
{
    private static readonly Granularity[] AllGranularities = [Granularity.Hour, Granularity.Day, Granularity.Week, Granularity.Month];

    public async Task<Unit> Handle(RunIncrementalProjectionCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var lastCompleted = await runRepository.GetLastCompletedAsync(cancellationToken);
        var reconciledThrough = lastCompleted?.RunDate.Value;

        foreach (var granularity in AllGranularities)
        {
            var bucketStart = BucketCalculator.GetBucketStart(now, granularity);
            var bucketEnd = now; // partial/in-progress bucket — recompute reflects exactly what's ingested so far.

            foreach (var projector in projectors)
            {
                try
                {
                    await projector.RecomputeAsync(bucketStart, bucketEnd, granularity, isProvisional: true, reconciledThrough, cancellationToken);
                }
                catch (Exception ex)
                {
                    // One projector's failure must not block the others — each dashboard is
                    // independent, so a transient issue in "revenue" shouldn't also stall
                    // "inventory-movement"'s otherwise-healthy incremental update.
                    logger.LogWarning(ex, "Incremental projection failed for {DashboardKey} at {Granularity}", projector.DashboardKey, granularity);
                }
            }
        }

        return Unit.Value;
    }
}
