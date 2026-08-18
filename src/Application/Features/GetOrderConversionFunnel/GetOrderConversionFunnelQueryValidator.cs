using FluentValidation;

namespace Kart.Analytics.Application.Features.GetOrderConversionFunnel;

public sealed class GetOrderConversionFunnelQueryValidator : AbstractValidator<GetOrderConversionFunnelQuery>
{
    public GetOrderConversionFunnelQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);
    }
}
