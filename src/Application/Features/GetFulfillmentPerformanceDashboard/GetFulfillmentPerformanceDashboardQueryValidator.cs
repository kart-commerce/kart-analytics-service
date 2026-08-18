using FluentValidation;

namespace Kart.Analytics.Application.Features.GetFulfillmentPerformanceDashboard;

public sealed class GetFulfillmentPerformanceDashboardQueryValidator : AbstractValidator<GetFulfillmentPerformanceDashboardQuery>
{
    public GetFulfillmentPerformanceDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
