namespace Kart.Analytics.Application.Common.Models;

/// <summary>
/// Shared logic every one of the ten dashboard/funnel query handlers uses to derive its response's
/// `DashboardEnvelope` from the bucket document(s) it fetched, instead of each handler
/// re-deriving the same "is any bucket still provisional" / "what's the weakest reconciled-through
/// guarantee across this window" logic independently.
/// </summary>
public static class DashboardEnvelopeBuilder
{
    public static DashboardEnvelope Build(IReadOnlyCollection<ReadModelBase> documents, DateTimeOffset generatedAt)
    {
        // No data at all for the requested window is treated as provisional too — there is
        // nothing yet to have been reconciled, so it would be misleading to report `false`.
        var isProvisional = documents.Count == 0 || documents.Any(d => d.IsProvisional);

        // If even one bucket in the window has never been reconciled (null), the whole window's
        // guarantee is null — Enumerable.Min on a nullable sequence silently ignores nulls, which
        // would wrongly imply the whole window was reconciled through some earlier date.
        DateOnly? reconciledThrough = documents.Count > 0 && documents.All(d => d.ReconciledThrough.HasValue)
            ? documents.Min(d => DateOnly.FromDateTime(d.ReconciledThrough!.Value))
            : null;

        return new DashboardEnvelope(generatedAt, isProvisional, reconciledThrough);
    }
}
