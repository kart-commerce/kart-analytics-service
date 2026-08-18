namespace Kart.Analytics.Application.Common.Models;

/// <summary>
/// api-contract.yaml's `DashboardEnvelope` schema — every dashboard/funnel response carries this,
/// surfacing the CQRS eventual-consistency window rather than hiding it
/// (ddd-cqrs-standards.md's "surface, don't hide" rule). Mirrors the same fields every Mongo read
/// model document itself carries (database-design.md).
/// </summary>
/// <param name="GeneratedAt">When this response was computed.</param>
/// <param name="IsProvisional">True until the nightly reconciliation run finalizes this bucket.</param>
/// <param name="ReconciledThrough">The date the last completed reconciliation run covers through; null if none has completed yet.</param>
public sealed record DashboardEnvelope(DateTimeOffset GeneratedAt, bool IsProvisional, DateOnly? ReconciledThrough);

/// <summary>api-contract.yaml's shared `Money` schema.</summary>
public sealed record Money(decimal Amount, string Currency);

/// <summary>api-contract.yaml's shared `DurationPercentiles` schema (fulfillment-performance dashboard).</summary>
public sealed record DurationPercentiles(double P50Hours, double P95Hours, double P99Hours);
