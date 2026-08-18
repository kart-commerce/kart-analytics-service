using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetReviewsRatingsDashboard;

public sealed class GetReviewsRatingsDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetReviewsRatingsDashboardQuery, ReviewsRatingsDashboardResult>
{
    private static readonly string[] RatingKeys = ["1", "2", "3", "4", "5"];

    public async Task<ReviewsRatingsDashboardResult> Handle(GetReviewsRatingsDashboardQuery request, CancellationToken cancellationToken)
    {
        var granularityLabel = BucketCalculator.ToGranularityLabel(request.Granularity);
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<ReviewsRatingsDashboardReadModel>(
            "reviews_ratings_dashboard",
            q => q.Where(d => d.Granularity == granularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc),
            cancellationToken);

        var distribution = RatingKeys.ToDictionary(key => key, key => documents.Sum(d => d.RatingDistribution.GetValueOrDefault(key)));
        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new ReviewsRatingsDashboardResult(envelope, documents.Sum(d => d.ReviewCount), distribution);
    }
}
