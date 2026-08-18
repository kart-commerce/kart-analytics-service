using MediatR;

namespace Kart.Analytics.Application.Features.RunIncrementalProjection;

/// <summary>
/// The near-real-time incremental projector half of the CQRS sync mechanism (database-design.md):
/// upserts provisional bucket documents for "right now" across every granularity, so dashboards
/// look live between nightly reconciliation runs. Always marks its output `isProvisional:true` —
/// only <c>RunNightlyReconciliationCommand</c> ever finalizes a bucket.
/// </summary>
public sealed record RunIncrementalProjectionCommand : IRequest<Unit>;
