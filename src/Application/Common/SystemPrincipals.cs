namespace Kart.Analytics.Application.Common;

/// <summary>
/// BRD §24.3's well-known <c>system:*</c> principal ids. Analytics has zero human/API-initiated
/// writes (requirement-spec.md — ingestion-only, no public write API) — every mutation to any of
/// the four Postgres aggregates or the Mongo read models originates from exactly one of this
/// service's own internal jobs, so `created_by`/`updated_by` is always one of these four constants,
/// never a caller-supplied value and never NULL (design-decisions.md "Audit Logging Requirements").
/// </summary>
public static class SystemPrincipals
{
    public const string IngestionConsumer = "system:analytics-ingestion-consumer";
    public const string DlqReprocessor = "system:analytics-dlq-reprocessor";
    public const string ReconciliationJob = "system:analytics-reconciliation-job";
    public const string PiiRedactionSweep = "system:analytics-pii-redaction-sweep";
}
