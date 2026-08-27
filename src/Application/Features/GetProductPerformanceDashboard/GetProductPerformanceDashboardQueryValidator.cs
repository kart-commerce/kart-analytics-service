using FluentValidation;

namespace Kart.Analytics.Application.Features.GetProductPerformanceDashboard;

public sealed class GetProductPerformanceDashboardQueryValidator : AbstractValidator<GetProductPerformanceDashboardQuery>
{
    private static readonly string[] ValidMetrics = ["revenue", "units_sold", "order_count"];
    private static readonly string[] ValidDirections = ["desc", "asc"];

    public GetProductPerformanceDashboardQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From);

        RuleFor(x => x.Metric)
            .Must(metric => ValidMetrics.Contains(metric))
            .WithMessage($"metric must be one of: {string.Join(", ", ValidMetrics)}.");

        RuleFor(x => x.Direction)
            .Must(direction => ValidDirections.Contains(direction))
            .WithMessage($"direction must be one of: {string.Join(", ", ValidDirections)}.");

        // api-contract.yaml: minimum 1 (a meaningless empty-ranking request), maximum 100 (sized
        // for a chat/table UI's top/bottom-N render, per edge-cases.md's "Note: `limit` Parameter
        // Bounds — No New Edge Case").
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}
