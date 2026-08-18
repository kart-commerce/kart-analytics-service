using FluentValidation;

namespace Kart.Analytics.Application.Features.GetNotificationDeliveryDashboard;

public sealed class GetNotificationDeliveryDashboardQueryValidator : AbstractValidator<GetNotificationDeliveryDashboardQuery>
{
    public GetNotificationDeliveryDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
