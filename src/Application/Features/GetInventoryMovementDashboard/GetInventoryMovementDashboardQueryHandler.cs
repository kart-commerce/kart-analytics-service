using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetInventoryMovementDashboard;

public sealed class GetInventoryMovementDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetInventoryMovementDashboardQuery, InventoryMovementDashboardResult>
{
    public async Task<InventoryMovementDashboardResult> Handle(GetInventoryMovementDashboardQuery request, CancellationToken cancellationToken)
    {
        var granularityLabel = BucketCalculator.ToGranularityLabel(request.Granularity);
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<InventoryMovementDashboardReadModel>(
            "inventory_movement_dashboard",
            q => q.Where(d => d.Granularity == granularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc && d.Sku == request.Sku),
            cancellationToken);

        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new InventoryMovementDashboardResult(
            envelope,
            documents.Sum(d => d.Reserved),
            documents.Sum(d => d.ReservationFailed),
            documents.Sum(d => d.Released),
            documents.Sum(d => d.Replenished));
    }
}
