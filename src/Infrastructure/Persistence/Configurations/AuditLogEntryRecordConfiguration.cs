using Kart.Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Analytics.Infrastructure.Persistence.Configurations;

/// <summary>This service's own operational audit trail — see <see cref="AuditLogEntryRecord"/>'s remarks.</summary>
public sealed class AuditLogEntryRecordConfiguration : IEntityTypeConfiguration<AuditLogEntryRecord>
{
    public void Configure(EntityTypeBuilder<AuditLogEntryRecord> builder)
    {
        builder.ToTable("analytics_audit_log");

        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId).HasColumnName("entry_id").ValueGeneratedNever();
        builder.Property(e => e.ServiceName).HasColumnName("service_name").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(e => e.ActorType).HasColumnName("actor_type").IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").IsRequired();
        builder.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");

        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAt })
            .HasDatabaseName("idx_analytics_audit_log_entity");
    }
}
