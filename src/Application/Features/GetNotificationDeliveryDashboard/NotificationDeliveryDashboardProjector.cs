using Kart.Analytics.Application.Common;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Domain.Entities;

namespace Kart.Analytics.Application.Features.GetNotificationDeliveryDashboard;

/// <summary>
/// ANL-15: notification_delivery_dashboard. `sent` is grouped by `NotificationSent.channel`;
/// `priceAlertsTriggered` (from `WishlistPriceAlertTriggered`, which carries no channel of its
/// own) is reported only on the bucket-wide total document (`Channel = null`).
/// </summary>
public sealed class NotificationDeliveryDashboardProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "notification-delivery";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var sent = await ingestedEventRepository.GetByTypeInWindowAsync("NotificationSent", windowFrom, windowTo, cancellationToken);
        var priceAlerts = await ingestedEventRepository.GetByTypeInWindowAsync("WishlistPriceAlertTriggered", windowFrom, windowTo, cancellationToken);

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var bucketPrefix = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        await Upsert(bucketPrefix, null, sent.Count, priceAlerts.Count, windowFrom, granularityLabel, isProvisional, reconciledThrough, cancellationToken);

        var channels = sent.Select(Channel).Where(c => c is not null).Distinct().ToList();
        foreach (var channel in channels)
        {
            var channelSentCount = sent.Count(e => Channel(e) == channel);
            await Upsert(bucketPrefix, channel, channelSentCount, 0, windowFrom, granularityLabel, isProvisional, reconciledThrough, cancellationToken);
        }
    }

    private static string? Channel(IngestedEvent e) => new PayloadReader(e.Payload).GetString("channel");

    private async Task Upsert(
        string bucketPrefix, string? channel, long sentCount, long priceAlertsCount,
        DateTimeOffset windowFrom, string granularityLabel, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var documentId = channel is null ? bucketPrefix : $"{bucketPrefix}:{channel}";

        var document = new NotificationDeliveryDashboardReadModel
        {
            Id = documentId,
            Granularity = granularityLabel,
            BucketStart = windowFrom.UtcDateTime,
            GeneratedAt = clock.UtcNow.UtcDateTime,
            IsProvisional = isProvisional,
            ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
            Channel = channel,
            Sent = sentCount,
            PriceAlertsTriggered = priceAlertsCount,
        };

        await readModelStore.UpsertAsync("notification_delivery_dashboard", documentId, document, cancellationToken);
    }
}
