using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;
using Kart.Analytics.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Analytics.Infrastructure.Persistence.Configurations;

/// <summary>
/// database-design.md `analytics_raw_events` — the <see cref="IngestedEvent"/> aggregate root.
/// The table's own `PARTITION BY RANGE (ingested_at)` clause and initial monthly partition are
/// NOT expressible through this configuration (EF Core has no native partitioned-table support)
/// — they are hand-written directly into the initial migration's raw SQL instead; this
/// configuration only owns the column/index shape EF's query translation relies on.
/// </summary>
public sealed class IngestedEventConfiguration : IEntityTypeConfiguration<IngestedEvent>
{
    public void Configure(EntityTypeBuilder<IngestedEvent> builder)
    {
        builder.ToTable("analytics_raw_events");

        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId)
            .HasColumnName("event_id")
            .HasConversion(TypedIdValueConverters.For<EventId>())
            .ValueGeneratedNever();

        builder.ComplexProperty(e => e.Envelope, envelope =>
        {
            envelope.Property(v => v.EventType).HasColumnName("event_type").IsRequired();
            envelope.Property(v => v.PublisherService).HasColumnName("publisher_service").IsRequired();
            envelope.Property(v => v.PartitionKey).HasColumnName("partition_key").IsRequired();
            envelope.Property(v => v.OccurredAt).HasColumnName("occurred_at").IsRequired();
        });

        builder.ComplexProperty(e => e.SchemaVersion, schema =>
        {
            schema.Property(v => v.SchemaId).HasColumnName("schema_id").IsRequired();
            schema.Property(v => v.VersionLabel).HasColumnName("schema_version_label").IsRequired();
        });

        builder.Property(e => e.IngestedAt).HasColumnName("ingested_at").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.ContainsPii).HasColumnName("contains_pii").IsRequired();
        builder.Property(e => e.PiiRedactedAt).HasColumnName("pii_redacted_at");

        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").IsRequired();

        // idx_analytics_raw_events_type_occurred (every projector's "events of type X since the
        // last watermark, in event-time order" scan) is added as raw SQL directly in the initial
        // migration instead of via this fluent API: EF Core's HasIndex lambda overload rejects a
        // multi-property anonymous-type expression that reaches into a ComplexProperty's nested
        // members ("not a valid member access expression") — a design-time-only limitation, not
        // a runtime one, so the index itself still exists and is used identically once created.

        // idx_analytics_raw_events_pii_pending — the redaction sweep's "find rows still needing
        // redaction for this user" scan, without a full-table scan across an indefinitely-retained
        // table (partial index, cheap since only a small fraction of rows carry PII).
        builder.HasIndex(e => e.ContainsPii)
            .HasDatabaseName("idx_analytics_raw_events_pii_pending")
            .HasFilter("contains_pii = true AND pii_redacted_at IS NULL");
    }
}
