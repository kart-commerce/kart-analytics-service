namespace Kart.Analytics.Infrastructure.Persistence;

/// <summary>
/// The real sink for `Kart.Shared.Auditing`'s <c>IAuditLogWriter</c> contract (see
/// <see cref="Auditing.PostgresAuditLogWriter"/>) — a plain persistence record, not a domain
/// aggregate, mapping 1:1 onto `Kart.Shared.Auditing.AuditLogEntry`. Deliberately NOT used for
/// every raw event ingestion (ANL-1) — that would duplicate `analytics_raw_events` itself at
/// full platform fan-in volume for no benefit. It is used for this service's own low-volume
/// operational actions: DLQ reprocessing, PII redaction sweeps, and reconciliation run
/// transitions — the same "who did what, when" trail BRD §24.3 asks for, at a volume where a
/// second table is actually worth its own write cost.
/// </summary>
public sealed class AuditLogEntryRecord
{
    public Guid EntryId { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public string ActorId { get; init; } = string.Empty;
    public string ActorType { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
    public string? MetadataJson { get; init; }
}
