using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetRevenueDashboard;

/// <summary>api-contract.yaml `GET /internal/v1/dashboards/revenue`.</summary>
public sealed record GetRevenueDashboardQuery(DateTimeOffset From, DateTimeOffset To, Granularity Granularity, string? Sku, string? Category) : IRequest<RevenueDashboardResult>;

public sealed record RevenueDashboardResult(DashboardEnvelope Envelope, IReadOnlyList<RevenueSeriesPoint> Series);

public sealed record RevenueSeriesPoint(DateTimeOffset BucketStart, Money Revenue, long OrderCount);
