using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetReviewsRatingsDashboard;

/// <summary>ANL-13: reviews_ratings_dashboard — count and 1-5 star histogram of `ReviewSubmitted`
/// in-window. `ReviewUpdated` (rating edits) is not folded into this histogram — a known
/// simplification, since the histogram would need to move a review's OLD rating out and its NEW
/// rating in, and `ReviewUpdated`'s documented payload does not carry which bucket the original
/// submission landed in.</summary>
public sealed class ReviewsRatingsDashboardProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "reviews-ratings";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var reviews = await ingestedEventRepository.GetByTypeInWindowAsync("ReviewSubmitted", windowFrom, windowTo, cancellationToken);

        var distribution = new Dictionary<string, long> { ["1"] = 0, ["2"] = 0, ["3"] = 0, ["4"] = 0, ["5"] = 0 };
        foreach (var review in reviews)
        {
            var rating = new PayloadReader(review.Payload).GetRatingInt("rating");
            if (rating is >= 1 and <= 5)
            {
                distribution[rating.ToString()]++;
            }
        }

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var documentId = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        var document = new ReviewsRatingsDashboardReadModel
        {
            Id = documentId,
            Granularity = granularityLabel,
            BucketStart = windowFrom.UtcDateTime,
            GeneratedAt = clock.UtcNow.UtcDateTime,
            IsProvisional = isProvisional,
            ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
            ReviewCount = reviews.Count,
            RatingDistribution = distribution,
        };

        await readModelStore.UpsertAsync("reviews_ratings_dashboard", documentId, document, cancellationToken);
    }
}
