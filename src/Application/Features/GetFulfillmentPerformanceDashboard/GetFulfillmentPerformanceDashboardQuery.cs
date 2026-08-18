using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetFulfillmentPerformanceDashboard;

/// <summary>api-contract.yaml `GET /internal/v1/dashboards/fulfillment-performance`.</summary>
public sealed record GetFulfillmentPerformanceDashboardQuery(DateTimeOffset From, DateTimeOffset To, Granularity Granularity) : IRequest<FulfillmentPerformanceDashboardResult>;

public sealed record FulfillmentPerformanceDashboardResult(DashboardEnvelope Envelope, DurationPercentiles TimeToShip, DurationPercentiles TimeToDeliver);
