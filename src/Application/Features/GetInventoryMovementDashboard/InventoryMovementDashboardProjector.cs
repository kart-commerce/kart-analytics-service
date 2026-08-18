using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Domain.Entities;

namespace Kart.Analytics.Application.Features.GetInventoryMovementDashboard;

/// <summary>ANL-9: inventory_movement_dashboard — one bucket-wide total document, plus one
/// per-SKU document for every SKU seen in-window (every source event carries `sku`).</summary>
public sealed class InventoryMovementDashboardProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "inventory-movement";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var reserved = await ingestedEventRepository.GetByTypeInWindowAsync("InventoryReserved", windowFrom, windowTo, cancellationToken);
        var reservationFailed = await ingestedEventRepository.GetByTypeInWindowAsync("InventoryReservationFailed", windowFrom, windowTo, cancellationToken);
        var released = await ingestedEventRepository.GetByTypeInWindowAsync("InventoryReleased", windowFrom, windowTo, cancellationToken);
        var replenished = await ingestedEventRepository.GetByTypeInWindowAsync("InventoryReplenished", windowFrom, windowTo, cancellationToken);

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var bucketPrefix = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        await UpsertRow(bucketPrefix, null, reserved.Count, reservationFailed.Count, released.Count, replenished.Count, windowFrom, granularityLabel, isProvisional, reconciledThrough, cancellationToken);

        var skus = reserved.Select(Sku).Concat(reservationFailed.Select(Sku)).Concat(released.Select(Sku)).Concat(replenished.Select(Sku))
            .Where(sku => sku is not null).Distinct().ToList();

        foreach (var sku in skus)
        {
            await UpsertRow(
                bucketPrefix,
                sku,
                reserved.Count(e => Sku(e) == sku),
                reservationFailed.Count(e => Sku(e) == sku),
                released.Count(e => Sku(e) == sku),
                replenished.Count(e => Sku(e) == sku),
                windowFrom, granularityLabel, isProvisional, reconciledThrough, cancellationToken);
        }
    }

    private static string? Sku(IngestedEvent e) => new PayloadReader(e.Payload).GetString("sku");

    private async Task UpsertRow(
        string bucketPrefix, string? sku, int reservedCount, int reservationFailedCount, int releasedCount, int replenishedCount,
        DateTimeOffset windowFrom, string granularityLabel, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var documentId = sku is null ? bucketPrefix : $"{bucketPrefix}:{sku}";

        var document = new InventoryMovementDashboardReadModel
        {
            Id = documentId,
            Granularity = granularityLabel,
            BucketStart = windowFrom.UtcDateTime,
            GeneratedAt = clock.UtcNow.UtcDateTime,
            IsProvisional = isProvisional,
            ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
            Sku = sku,
            Reserved = reservedCount,
            ReservationFailed = reservationFailedCount,
            Released = releasedCount,
            Replenished = replenishedCount,
        };

        await readModelStore.UpsertAsync("inventory_movement_dashboard", documentId, document, cancellationToken);
    }
}
