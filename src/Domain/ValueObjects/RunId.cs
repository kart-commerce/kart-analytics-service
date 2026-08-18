namespace Kart.Analytics.Domain.ValueObjects;

/// <summary>
/// Strongly-typed technical identifier for the <see cref="Entities.ReconciliationRun"/> aggregate
/// root — database-design.md `analytics_reconciliation_runs.run_id`. ddd-model.md names the
/// aggregate's conceptual/business identity as <see cref="RunDate"/> (at most one run per calendar
/// date, enforced by the table's own `UNIQUE(run_date)`); this is the surrogate primary key the
/// physical schema actually uses.
/// </summary>
public readonly record struct RunId(Guid Value) : ITypedEntityId<RunId>
{
    public static RunId New() => new(Guid.NewGuid());

    public static RunId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
