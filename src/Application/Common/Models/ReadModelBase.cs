namespace Kart.Analytics.Application.Common.Models;

/// <summary>
/// Fields every one of the ten dashboard/funnel collections carries — matches
/// `api-contract.yaml`'s `DashboardEnvelope` (database-design.md), so the CQRS eventual-consistency
/// window is surfaced in the read model itself, not bolted on later ("surface, don't hide"). Plain
/// POCO, no MongoDB type — the Mongo driver's default convention maps a member literally named
/// <c>Id</c> to the document's `_id` field without needing a `[BsonId]` attribute, which is what
/// keeps this class (and everything that extends it) free of any Infrastructure/MongoDB.Bson
/// dependency in the Application layer.
/// </summary>
public abstract class ReadModelBase
{
    public string Id { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }

    public bool IsProvisional { get; set; } = true;

    public DateTime? ReconciledThrough { get; set; }
}

/// <summary>The nine time-bucketed dashboard/funnel collections all add these two fields on top
/// of the shared envelope — `admin_audit_log` is the one exception (a log, not a bucket aggregate;
/// see `GetAdminAuditDashboard.AdminAuditLogReadModel`).</summary>
public abstract class BucketedReadModelBase : ReadModelBase
{
    public string Granularity { get; set; } = string.Empty;

    public DateTime BucketStart { get; set; }
}
