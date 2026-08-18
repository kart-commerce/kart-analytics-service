using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kart.Analytics.Infrastructure.Persistence.Repositories;

/// <summary>The sole repository for the <see cref="IngestedEvent"/> aggregate root.</summary>
public sealed class IngestedEventRepository(AnalyticsDbContext dbContext) : IIngestedEventRepository
{
    public async Task<IngestedEvent?> FindByIdAsync(EventId eventId, CancellationToken cancellationToken) =>
        await dbContext.IngestedEvents.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId, cancellationToken);

    /// <summary>
    /// Atomic `INSERT ... ON CONFLICT (event_id) DO UPDATE` — the single database-level operation
    /// that makes this idempotent under concurrent redelivery of the same event id (no
    /// find-then-branch race window). `ingested_at` (first-landing time) is never touched by the
    /// UPDATE branch; `pii_redacted_at`/`payload` are preserved verbatim when the existing row was
    /// already redacted, so a replay of the original un-redacted payload can never undo a
    /// compliance redaction (edge-cases.md "Replay Correctness" + database-design.md's redaction
    /// decision, applied together). Uses Postgres' `xmax = 0` trick to report, in the same round
    /// trip, whether this call performed a fresh insert or overwrote an existing row.
    /// </summary>
    public async Task<bool> UpsertAsync(IngestedEvent ingestedEvent, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO analytics_raw_events
                (event_id, event_type, publisher_service, partition_key, occurred_at,
                 schema_id, schema_version_label, ingested_at, payload, contains_pii,
                 pii_redacted_at, created_by, updated_at, updated_by)
            VALUES
                (@event_id, @event_type, @publisher_service, @partition_key, @occurred_at,
                 @schema_id, @schema_version_label, @ingested_at, @payload::jsonb, @contains_pii,
                 NULL, @created_by, @updated_at, @updated_by)
            ON CONFLICT (event_id) DO UPDATE SET
                event_type = EXCLUDED.event_type,
                publisher_service = EXCLUDED.publisher_service,
                partition_key = EXCLUDED.partition_key,
                occurred_at = EXCLUDED.occurred_at,
                schema_id = EXCLUDED.schema_id,
                schema_version_label = EXCLUDED.schema_version_label,
                payload = CASE WHEN analytics_raw_events.pii_redacted_at IS NOT NULL
                               THEN analytics_raw_events.payload
                               ELSE EXCLUDED.payload END,
                contains_pii = EXCLUDED.contains_pii,
                updated_at = EXCLUDED.updated_at,
                updated_by = EXCLUDED.updated_by
            RETURNING (xmax = 0) AS was_inserted;
            """;

        await using var command = new NpgsqlCommand(sql)
        {
            Parameters =
            {
                new NpgsqlParameter("event_id", ingestedEvent.EventId.Value),
                new NpgsqlParameter("event_type", ingestedEvent.Envelope.EventType),
                new NpgsqlParameter("publisher_service", ingestedEvent.Envelope.PublisherService),
                new NpgsqlParameter("partition_key", ingestedEvent.Envelope.PartitionKey),
                new NpgsqlParameter("occurred_at", ingestedEvent.Envelope.OccurredAt),
                new NpgsqlParameter("schema_id", ingestedEvent.SchemaVersion.SchemaId),
                new NpgsqlParameter("schema_version_label", ingestedEvent.SchemaVersion.VersionLabel),
                new NpgsqlParameter("ingested_at", ingestedEvent.IngestedAt),
                new NpgsqlParameter("payload", ingestedEvent.Payload),
                new NpgsqlParameter("contains_pii", ingestedEvent.ContainsPii),
                new NpgsqlParameter("created_by", ingestedEvent.CreatedBy),
                new NpgsqlParameter("updated_at", ingestedEvent.UpdatedAt),
                new NpgsqlParameter("updated_by", ingestedEvent.UpdatedBy),
            },
        };

        return await ExecuteScalarBoolAsync(command, cancellationToken);
    }

    /// <summary>
    /// Two-step rather than a single `FromSqlInterpolated("SELECT * ...")`: EF Core 8's
    /// `ComplexProperty` mapping (<see cref="IngestedEvent.Envelope"/>/<see cref="IngestedEvent.SchemaVersion"/>)
    /// expects a raw-SQL result set's column names to match its own internal materialization
    /// shape, which a plain `SELECT *` does not satisfy ("required column ... was not present in
    /// the results of a 'FromSql' operation") — a design-time/runtime quirk of complex types, not
    /// a reason to decompose the payload into first-class columns just to work around it. The
    /// `payload ->> 'userId'` JSONB filter (not translatable by plain LINQ) runs as a scalar-only
    /// raw query; the matching rows are then materialized through the ordinary, already-working
    /// LINQ path (same as <see cref="GetByTypeInWindowAsync"/>).
    /// </summary>
    public async Task<IReadOnlyList<IngestedEvent>> GetPiiPendingForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var matchingIds = await dbContext.Database
            .SqlQuery<Guid>($"""
                SELECT event_id FROM analytics_raw_events
                WHERE contains_pii = true
                  AND pii_redacted_at IS NULL
                  AND payload ->> 'userId' = {userId}
                """)
            .ToListAsync(cancellationToken);

        if (matchingIds.Count == 0)
        {
            return [];
        }

        var matchingEventIds = matchingIds.Select(EventId.From).ToList();
        return await dbContext.IngestedEvents
            .Where(e => matchingEventIds.Contains(e.EventId))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveRedactedBatchAsync(IReadOnlyList<IngestedEvent> redactedEvents, CancellationToken cancellationToken)
    {
        dbContext.IngestedEvents.UpdateRange(redactedEvents);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IngestedEvent>> GetByTypeSinceAsync(string eventType, DateTimeOffset since, CancellationToken cancellationToken) =>
        await dbContext.IngestedEvents
            .AsNoTracking()
            .Where(e => e.Envelope.EventType == eventType && e.Envelope.OccurredAt >= since)
            .OrderBy(e => e.Envelope.OccurredAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IngestedEvent>> GetByTypeInWindowAsync(string eventType, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await dbContext.IngestedEvents
            .AsNoTracking()
            .Where(e => e.Envelope.EventType == eventType && e.Envelope.OccurredAt >= from && e.Envelope.OccurredAt < to)
            .OrderBy(e => e.Envelope.OccurredAt)
            .ToListAsync(cancellationToken);

    private async Task<bool> ExecuteScalarBoolAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        command.Connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var wasAlreadyOpen = command.Connection.State == System.Data.ConnectionState.Open;
        if (!wasAlreadyOpen)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool wasInserted && wasInserted;
        }
        finally
        {
            if (!wasAlreadyOpen)
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
