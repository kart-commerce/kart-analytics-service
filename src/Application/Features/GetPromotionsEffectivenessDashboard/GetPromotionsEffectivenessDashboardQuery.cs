using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetPromotionsEffectivenessDashboard;

/// <summary>api-contract.yaml `GET /internal/v1/dashboards/promotions-effectiveness`.</summary>
public sealed record GetPromotionsEffectivenessDashboardQuery(DateTimeOffset From, DateTimeOffset To, Granularity Granularity) : IRequest<PromotionsEffectivenessDashboardResult>;

public sealed record PromotionsEffectivenessDashboardResult(DashboardEnvelope Envelope, long CouponsRedeemed, long QuotesIssued, Money AttributableOrderVolume, double RedemptionRate);
