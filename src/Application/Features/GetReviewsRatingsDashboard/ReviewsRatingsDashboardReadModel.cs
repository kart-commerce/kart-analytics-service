using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetReviewsRatingsDashboard;

/// <summary>database-design.md `reviews_ratings_dashboard` — backs `GET /internal/v1/dashboards/reviews-ratings` (ANL-13).</summary>
public sealed class ReviewsRatingsDashboardReadModel : BucketedReadModelBase
{
    public long ReviewCount { get; set; }
    public Dictionary<string, long> RatingDistribution { get; set; } = new();
}
