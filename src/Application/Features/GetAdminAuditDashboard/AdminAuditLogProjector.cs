using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetAdminAuditDashboard;

/// <summary>ANL-14: admin_audit_log. Unlike the other nine projectors, this is a log (one document
/// per source `AdminActionPerformed` event, keyed by that event's own `event_id`), not a bucketed
/// aggregate — `granularity` is accepted for interface uniformity but unused.</summary>
public sealed class AdminAuditLogProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "admin-audit";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var actions = await ingestedEventRepository.GetByTypeInWindowAsync("AdminActionPerformed", windowFrom, windowTo, cancellationToken);

        foreach (var action in actions)
        {
            var payload = new PayloadReader(action.Payload);
            var documentId = action.EventId.ToString();

            var document = new AdminAuditLogReadModel
            {
                Id = documentId,
                GeneratedAt = clock.UtcNow.UtcDateTime,
                IsProvisional = isProvisional,
                ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
                OccurredAt = action.Envelope.OccurredAt.UtcDateTime,
                ActionType = payload.GetString("action") ?? "unknown",
                AdminId = payload.GetString("adminId") ?? "unknown",
            };

            await readModelStore.UpsertAsync("admin_audit_log", documentId, document, cancellationToken);
        }
    }
}
