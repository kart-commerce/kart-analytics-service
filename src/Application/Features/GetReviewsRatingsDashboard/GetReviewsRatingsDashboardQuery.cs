using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetReviewsRatingsDashboard;

/// <summary>api-contract.yaml `GET /internal/v1/dashboards/reviews-ratings`.</summary>
public sealed record GetReviewsRatingsDashboardQuery(DateTimeOffset From, DateTimeOffset To, Granularity Granularity) : IRequest<ReviewsRatingsDashboardResult>;

public sealed record ReviewsRatingsDashboardResult(DashboardEnvelope Envelope, long ReviewCount, IReadOnlyDictionary<string, long> RatingDistribution);
