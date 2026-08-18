using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Common.Interfaces;

/// <summary>
/// One implementation per D4a dashboard/funnel (ANL-6..ANL-15), each fully recomputing its own
/// bucket(s) from `analytics_raw_events` for the given window — never incrementally mutated
/// (design-decisions.md "Idempotency Mechanism for Replay-Safe Aggregation"). Registered as a DI
/// collection; both <c>RunNightlyReconciliationCommandHandler</c> and
/// <c>RunIncrementalProjectionCommandHandler</c> iterate every registered projector, so adding an
/// 11th dashboard later only means registering another implementation — neither job's own code
/// changes (OCP).
/// </summary>
public interface IReadModelProjector
{
    /// <summary>For logging/diagnostics only, e.g. "revenue".</summary>
    string DashboardKey { get; }

    /// <summary>
    /// Recomputes this dashboard's bucket(s) for the full window [<paramref name="windowFrom"/>,
    /// <paramref name="windowTo"/>) at the given granularity, from raw storage, and upserts the
    /// result via <see cref="IReadModelWriter"/>.
    /// </summary>
    Task RecomputeAsync(
        DateTimeOffset windowFrom,
        DateTimeOffset windowTo,
        Granularity granularity,
        bool isProvisional,
        DateOnly? reconciledThrough,
        CancellationToken cancellationToken);
}
