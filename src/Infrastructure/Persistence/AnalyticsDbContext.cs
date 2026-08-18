using Kart.Analytics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kart.Analytics.Infrastructure.Persistence;

/// <summary>
/// EF Core write-side context — database-design.md's four PostgreSQL tables. This is Analytics'
/// only database write path; the ten MongoDB read models are written exclusively by the
/// projection consumers (Infrastructure/Projections), never through this context.
/// </summary>
public sealed class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<IngestedEvent> IngestedEvents => Set<IngestedEvent>();
    public DbSet<DeadLetteredEvent> DeadLetteredEvents => Set<DeadLetteredEvent>();
    public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();
    public DbSet<PiiRedactionRecord> PiiRedactionRecords => Set<PiiRedactionRecord>();
    public DbSet<AuditLogEntryRecord> AuditLogEntries => Set<AuditLogEntryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnalyticsDbContext).Assembly);
    }
}
