using Kart.Analytics.Application.Common.Interfaces;
using MediatR;

namespace Kart.Analytics.Application.Features.GetAdminAuditDashboard;

public sealed class GetAdminAuditDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetAdminAuditDashboardQuery, AdminAuditDashboardResult>
{
    public async Task<AdminAuditDashboardResult> Handle(GetAdminAuditDashboardQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<AdminAuditLogReadModel>(
            "admin_audit_log",
            q =>
            {
                var filtered = q.Where(d => d.OccurredAt >= fromUtc && d.OccurredAt < toUtc);
                return request.ActionType is null ? filtered : filtered.Where(d => d.ActionType == request.ActionType);
            },
            cancellationToken);

        var actions = documents
            .OrderBy(d => d.OccurredAt)
            .Select(d => new AdminActionResult(new DateTimeOffset(d.OccurredAt, TimeSpan.Zero), d.ActionType, d.AdminId))
            .ToList();

        var envelope = Kart.Analytics.Application.Common.Models.DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new AdminAuditDashboardResult(envelope, actions);
    }
}
