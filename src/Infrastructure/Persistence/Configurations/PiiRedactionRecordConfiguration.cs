using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;
using Kart.Analytics.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Analytics.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `analytics_pii_redactions` — the immutable <see cref="PiiRedactionRecord"/> aggregate root.</summary>
public sealed class PiiRedactionRecordConfiguration : IEntityTypeConfiguration<PiiRedactionRecord>
{
    public void Configure(EntityTypeBuilder<PiiRedactionRecord> builder)
    {
        builder.ToTable("analytics_pii_redactions");

        builder.HasKey(r => r.RedactionId);
        builder.Property(r => r.RedactionId)
            .HasColumnName("redaction_id")
            .HasConversion(TypedIdValueConverters.For<RedactionId>())
            .ValueGeneratedNever();

        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(r => r.TriggeringEventId)
            .HasColumnName("triggering_event_id")
            .HasConversion(TypedIdValueConverters.For<EventId>())
            .IsRequired();

        builder.Property(r => r.RowsRedacted).HasColumnName("rows_redacted").IsRequired();
        builder.Property(r => r.RedactedAt).HasColumnName("redacted_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").IsRequired();

        // idx_analytics_pii_redactions_user — the compliance lookup "has this user's data been
        // redacted, and when."
        builder.HasIndex(r => new { r.UserId, r.RedactedAt })
            .HasDatabaseName("idx_analytics_pii_redactions_user");
    }
}
