namespace Kart.Analytics.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for the <see cref="Entities.DeadLetteredEvent"/> aggregate root —
/// database-design.md `analytics_dlq_events.dlq_id`. Deliberately distinct from
/// <see cref="EventId"/> (ddd-model.md): a dead-lettered row references the event that failed to
/// write by value, never by foreign key, so its own identity is independent.
/// </summary>
public readonly record struct DlqId(Guid Value) : ITypedEntityId<DlqId>
{
    public static DlqId New() => new(Guid.NewGuid());

    public static DlqId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
