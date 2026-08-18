using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetUserGrowthDashboard;

/// <summary>api-contract.yaml `GET /internal/v1/dashboards/user-growth`.</summary>
public sealed record GetUserGrowthDashboardQuery(DateTimeOffset From, DateTimeOffset To, Granularity Granularity) : IRequest<UserGrowthDashboardResult>;

public sealed record UserGrowthDashboardResult(DashboardEnvelope Envelope, long Signups, long SessionsCreated, long ProfileChanges);
