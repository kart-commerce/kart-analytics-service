namespace Kart.Analytics.Domain.ValueObjects;

/// <summary>
/// ddd-model.md's conceptual identity for the <see cref="Entities.ReconciliationRun"/> aggregate —
/// the calendar event-time date one nightly batch run reconciles. database-design.md enforces at
/// most one run per date via `UNIQUE(run_date)`.
/// </summary>
public readonly record struct RunDate(DateOnly Value)
{
    public static RunDate From(DateOnly value) => new(value);

    public override string ToString() => Value.ToString("yyyy-MM-dd");
}
