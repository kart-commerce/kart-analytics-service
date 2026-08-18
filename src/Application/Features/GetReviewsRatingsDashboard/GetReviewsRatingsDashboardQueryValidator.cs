using FluentValidation;

namespace Kart.Analytics.Application.Features.GetReviewsRatingsDashboard;

public sealed class GetReviewsRatingsDashboardQueryValidator : AbstractValidator<GetReviewsRatingsDashboardQuery>
{
    public GetReviewsRatingsDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
