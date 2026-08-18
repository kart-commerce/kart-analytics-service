using FluentValidation;

namespace Kart.Analytics.Application.Features.GetCatalogPricingDashboard;

public sealed class GetCatalogPricingDashboardQueryValidator : AbstractValidator<GetCatalogPricingDashboardQuery>
{
    public GetCatalogPricingDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
