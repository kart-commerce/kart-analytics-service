using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetOrderConversionFunnel;

/// <summary>api-contract.yaml `GET /internal/v1/funnels/order-conversion`.</summary>
public sealed record GetOrderConversionFunnelQuery(DateTimeOffset From, DateTimeOffset To, Granularity Granularity) : IRequest<OrderConversionFunnelResult>;

public sealed record OrderConversionFunnelResult(DashboardEnvelope Envelope, IReadOnlyList<FunnelStageResult> Stages);

public sealed record FunnelStageResult(string Stage, long Count, double? DropOffRate);
