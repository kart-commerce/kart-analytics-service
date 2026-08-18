using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetNotificationDeliveryDashboard;

public sealed class GetNotificationDeliveryDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetNotificationDeliveryDashboardQuery, NotificationDeliveryDashboardResult>
{
    public async Task<NotificationDeliveryDashboardResult> Handle(GetNotificationDeliveryDashboardQuery request, CancellationToken cancellationToken)
    {
        var granularityLabel = BucketCalculator.ToGranularityLabel(request.Granularity);
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<NotificationDeliveryDashboardReadModel>(
            "notification_delivery_dashboard",
            q =>
            {
                var filtered = q.Where(d => d.Granularity == granularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc);
                return request.Channel is null ? filtered : filtered.Where(d => d.Channel == request.Channel);
            },
            cancellationToken);

        var byChannel = documents
            .GroupBy(d => d.Channel)
            .Select(g => new NotificationChannelResult(g.Key, g.Sum(d => d.Sent), g.Sum(d => d.PriceAlertsTriggered)))
            .ToList();

        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new NotificationDeliveryDashboardResult(envelope, byChannel);
    }
}
