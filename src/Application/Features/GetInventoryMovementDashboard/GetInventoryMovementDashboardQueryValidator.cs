using FluentValidation;

namespace Kart.Analytics.Application.Features.GetInventoryMovementDashboard;

public sealed class GetInventoryMovementDashboardQueryValidator : AbstractValidator<GetInventoryMovementDashboardQuery>
{
    public GetInventoryMovementDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
