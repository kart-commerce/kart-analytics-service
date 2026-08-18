using FluentValidation;

namespace Kart.Analytics.Application.Features.GetPromotionsEffectivenessDashboard;

public sealed class GetPromotionsEffectivenessDashboardQueryValidator : AbstractValidator<GetPromotionsEffectivenessDashboardQuery>
{
    public GetPromotionsEffectivenessDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
