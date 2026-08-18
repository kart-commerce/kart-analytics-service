using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetInventoryMovementDashboard;

/// <summary>api-contract.yaml `GET /internal/v1/dashboards/inventory-movement`.</summary>
public sealed record GetInventoryMovementDashboardQuery(DateTimeOffset From, DateTimeOffset To, Granularity Granularity, string? Sku) : IRequest<InventoryMovementDashboardResult>;

public sealed record InventoryMovementDashboardResult(DashboardEnvelope Envelope, long Reserved, long ReservationFailed, long Released, long Replenished);
