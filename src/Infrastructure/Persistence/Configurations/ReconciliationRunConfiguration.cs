using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.Enums;
using Kart.Analytics.Domain.ValueObjects;
using Kart.Analytics.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Analytics.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `analytics_reconciliation_runs` — the <see cref="ReconciliationRun"/> aggregate root.</summary>
public sealed class ReconciliationRunConfiguration : IEntityTypeConfiguration<ReconciliationRun>
{
    public void Configure(EntityTypeBuilder<ReconciliationRun> builder)
    {
        builder.ToTable("analytics_reconciliation_runs", t => t.HasCheckConstraint(
            "ck_analytics_reconciliation_runs_status", "status IN ('running', 'completed', 'failed')"));

        builder.HasKey(r => r.RunId);
        builder.Property(r => r.RunId)
            .HasColumnName("run_id")
            .HasConversion(TypedIdValueConverters.For<RunId>())
            .ValueGeneratedNever();

        builder.Property(r => r.RunDate)
            .HasColumnName("run_date")
            .HasConversion(DomainValueConverters.RunDate)
            .IsRequired();
        builder.HasIndex(r => r.RunDate).IsUnique().HasDatabaseName("uq_analytics_reconciliation_runs_run_date");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(EnumDbValueConverters.RunStatus)
            .IsRequired();

        builder.Property(r => r.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");

        builder.Property(r => r.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").IsRequired();
    }
}
