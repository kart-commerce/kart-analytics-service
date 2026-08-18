using FluentValidation;

namespace Kart.Analytics.Application.Features.GetUserGrowthDashboard;

public sealed class GetUserGrowthDashboardQueryValidator : AbstractValidator<GetUserGrowthDashboardQuery>
{
    public GetUserGrowthDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
