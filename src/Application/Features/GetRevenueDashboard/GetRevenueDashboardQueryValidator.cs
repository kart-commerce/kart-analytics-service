using FluentValidation;

namespace Kart.Analytics.Application.Features.GetRevenueDashboard;

public sealed class GetRevenueDashboardQueryValidator : AbstractValidator<GetRevenueDashboardQuery>
{
    public GetRevenueDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
