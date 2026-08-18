using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetAdminAuditDashboard;

/// <summary>api-contract.yaml `GET /internal/v1/dashboards/admin-audit` — no `Granularity` param (a log query, not a bucket aggregate).</summary>
public sealed record GetAdminAuditDashboardQuery(DateTimeOffset From, DateTimeOffset To, string? ActionType) : IRequest<AdminAuditDashboardResult>;

public sealed record AdminAuditDashboardResult(DashboardEnvelope Envelope, IReadOnlyList<AdminActionResult> Actions);

public sealed record AdminActionResult(DateTimeOffset OccurredAt, string ActionType, string AdminId);
