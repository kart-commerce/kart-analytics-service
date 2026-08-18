using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetOrderConversionFunnel;

public sealed class GetOrderConversionFunnelQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetOrderConversionFunnelQuery, OrderConversionFunnelResult>
{
    private static readonly string[] StageOrder = ["CartCheckedOut", "OrderCreated", "OrderConfirmed", "PaymentCompleted", "OrderDelivered"];

    public async Task<OrderConversionFunnelResult> Handle(GetOrderConversionFunnelQuery request, CancellationToken cancellationToken)
    {
        var granularityLabel = BucketCalculator.ToGranularityLabel(request.Granularity);
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<OrderConversionFunnelReadModel>(
            "order_conversion_funnel",
            q => q.Where(d => d.Granularity == granularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc),
            cancellationToken);

        var countsByStage = StageOrder.ToDictionary(stage => stage, _ => 0L);
        foreach (var stage in documents.SelectMany(d => d.Stages))
        {
            if (countsByStage.ContainsKey(stage.Stage))
            {
                countsByStage[stage.Stage] += stage.Count;
            }
        }

        var stages = new List<FunnelStageResult>(StageOrder.Length);
        for (var i = 0; i < StageOrder.Length; i++)
        {
            var count = countsByStage[StageOrder[i]];
            double? dropOffRate = i == 0 || countsByStage[StageOrder[i - 1]] == 0 ? null : 1.0 - (double)count / countsByStage[StageOrder[i - 1]];
            stages.Add(new FunnelStageResult(StageOrder[i], count, dropOffRate));
        }

        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new OrderConversionFunnelResult(envelope, stages);
    }
}
