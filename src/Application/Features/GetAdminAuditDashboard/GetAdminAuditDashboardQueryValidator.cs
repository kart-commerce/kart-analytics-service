using FluentValidation;

namespace Kart.Analytics.Application.Features.GetAdminAuditDashboard;

public sealed class GetAdminAuditDashboardQueryValidator : AbstractValidator<GetAdminAuditDashboardQuery>
{
    public GetAdminAuditDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
