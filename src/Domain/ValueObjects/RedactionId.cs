namespace Kart.Analytics.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for the <see cref="Entities.PiiRedactionRecord"/> aggregate root —
/// database-design.md `analytics_pii_redactions.redaction_id`. The record itself is immutable
/// once written (ddd-model.md invariant): a later sweep for the same user creates a new row with a
/// new <see cref="RedactionId"/>, never mutates an old one.
/// </summary>
public readonly record struct RedactionId(Guid Value) : ITypedEntityId<RedactionId>
{
    public static RedactionId New() => new(Guid.NewGuid());

    public static RedactionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
