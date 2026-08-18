using System.Text.Json;
using Kart.Analytics.Infrastructure.Persistence;
using Kart.Shared.Auditing;

namespace Kart.Analytics.Infrastructure.Auditing;

/// <summary>
/// A REAL `IAuditLogWriter` sink (`analytics_audit_log` table) — not the
/// `Kart.Shared.Auditing.NullAuditLogWriter` default, mirroring kart-recommendation-service's own
/// `MongoAuditLogWriter` precedent for a service with a genuine, explicit audit-trail requirement.
/// See <see cref="AuditLogEntryRecord"/>'s remarks for why this is scoped to this service's own
/// low-volume operational actions, not every raw event ingestion.
/// </summary>
public sealed class PostgresAuditLogWriter(AnalyticsDbContext dbContext) : IAuditLogWriter
{
    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        var record = new AuditLogEntryRecord
        {
            EntryId = entry.EntryId,
            ServiceName = entry.ServiceName,
            ActorId = entry.ActorId,
            ActorType = entry.ActorType,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            OccurredAt = entry.OccurredAt,
            MetadataJson = entry.Metadata is { Count: > 0 } ? JsonSerializer.Serialize(entry.Metadata) : null,
        };

        dbContext.AuditLogEntries.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
