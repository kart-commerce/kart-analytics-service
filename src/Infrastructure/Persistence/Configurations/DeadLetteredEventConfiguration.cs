using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;
using Kart.Analytics.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Analytics.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `analytics_dlq_events` — the <see cref="DeadLetteredEvent"/> aggregate root.</summary>
public sealed class DeadLetteredEventConfiguration : IEntityTypeConfiguration<DeadLetteredEvent>
{
    public void Configure(EntityTypeBuilder<DeadLetteredEvent> builder)
    {
        builder.ToTable("analytics_dlq_events");

        builder.HasKey(e => e.DlqId);
        builder.Property(e => e.DlqId)
            .HasColumnName("dlq_id")
            .HasConversion(TypedIdValueConverters.For<DlqId>())
            .ValueGeneratedNever();

        builder.Property(e => e.EventId)
            .HasColumnName("event_id")
            .HasConversion(TypedIdValueConverters.For<EventId>())
            .IsRequired();

        builder.Property(e => e.EventType).HasColumnName("event_type").IsRequired();

        // Deliberately `text`, not `jsonb` — unlike `analytics_raw_events.payload` (always a
        // schema-registry/tolerant-reader-validated event), a DLQ row's whole purpose is to
        // preserve whatever bytes actually arrived, including a payload that isn't valid JSON at
        // all (edge-cases.md "Schema Evolution" — a genuinely malformed message is exactly the
        // case this table exists to capture for forensic inspection/reprocessing). A `jsonb`
        // column would make Postgres itself reject the insert for the one case this table must
        // never lose.
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("text").IsRequired();

        builder.Property(e => e.FailureReason).HasColumnName("failure_reason").IsRequired();
        builder.Property(e => e.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(e => e.DlqLandedAt).HasColumnName("dlq_landed_at").IsRequired();
        builder.Property(e => e.ReprocessedAt).HasColumnName("reprocessed_at");

        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").IsRequired();

        // idx_analytics_dlq_events_pending — the reprocessor's own "find everything still parked,
        // oldest first" drain scan.
        builder.HasIndex(e => e.DlqLandedAt)
            .HasDatabaseName("idx_analytics_dlq_events_pending")
            .HasFilter("reprocessed_at IS NULL");
    }
}
