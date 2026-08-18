using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetNotificationDeliveryDashboard;

/// <summary>api-contract.yaml `GET /internal/v1/dashboards/notification-delivery`.</summary>
public sealed record GetNotificationDeliveryDashboardQuery(DateTimeOffset From, DateTimeOffset To, Granularity Granularity, string? Channel) : IRequest<NotificationDeliveryDashboardResult>;

public sealed record NotificationDeliveryDashboardResult(DashboardEnvelope Envelope, IReadOnlyList<NotificationChannelResult> ByChannel);

public sealed record NotificationChannelResult(string? Channel, long Sent, long PriceAlertsTriggered);
