using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetOrderConversionFunnel;

/// <summary>database-design.md `order_conversion_funnel` — backs `GET /internal/v1/funnels/order-conversion` (ANL-6).</summary>
public sealed class OrderConversionFunnelReadModel : BucketedReadModelBase
{
    public List<FunnelStageReadModel> Stages { get; set; } = [];
}

public sealed class FunnelStageReadModel
{
    public string Stage { get; set; } = string.Empty;
    public long Count { get; set; }
    public double? DropOffRate { get; set; }
}
