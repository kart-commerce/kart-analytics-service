using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Application.Features.GetAdminAuditDashboard;
using Kart.Analytics.Application.Features.GetCatalogPricingDashboard;
using Kart.Analytics.Application.Features.GetFulfillmentPerformanceDashboard;
using Kart.Analytics.Application.Features.GetInventoryMovementDashboard;
using Kart.Analytics.Application.Features.GetNotificationDeliveryDashboard;
using Kart.Analytics.Application.Features.GetOrderConversionFunnel;
using Kart.Analytics.Application.Features.GetProductPerformanceDashboard;
using Kart.Analytics.Application.Features.GetPromotionsEffectivenessDashboard;
using Kart.Analytics.Application.Features.GetReviewsRatingsDashboard;
using Kart.Analytics.Application.Features.GetRevenueDashboard;
using Kart.Analytics.Application.Features.GetUserGrowthDashboard;
using MediatR;

namespace Kart.Analytics.Api.Endpoints;

/// <summary>
/// api-contract.yaml's eleven `/internal/v1/{dashboards,funnels}/*` endpoints (ten original plus
/// `product-performance`, added this pass) — every one GET-only, gated by the
/// `analytics.dashboards.read` scope policy (Program.cs), never routed through the public API
/// Gateway (requirement-spec.md §1).
/// </summary>
public static class DashboardEndpoints
{
    private const string RequiredScopePolicy = "AnalyticsDashboardsRead";

    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/internal/v1").RequireAuthorization(RequiredScopePolicy);

        group.MapGet("/funnels/order-conversion", async (ISender sender, DateTimeOffset from, DateTimeOffset to, Granularity granularity = Granularity.Day) =>
        {
            var result = await sender.Send(new GetOrderConversionFunnelQuery(from, to, granularity));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, result.Stages });
        });

        group.MapGet("/dashboards/revenue", async (ISender sender, DateTimeOffset from, DateTimeOffset to, Granularity granularity, string? sku, string? category) =>
        {
            var result = await sender.Send(new GetRevenueDashboardQuery(from, to, granularity, sku, category));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, result.Series });
        });

        group.MapGet("/dashboards/fulfillment-performance", async (ISender sender, DateTimeOffset from, DateTimeOffset to, Granularity granularity = Granularity.Day) =>
        {
            var result = await sender.Send(new GetFulfillmentPerformanceDashboardQuery(from, to, granularity));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, timeToShip = result.TimeToShip, timeToDeliver = result.TimeToDeliver });
        });

        group.MapGet("/dashboards/inventory-movement", async (ISender sender, DateTimeOffset from, DateTimeOffset to, Granularity granularity, string? sku) =>
        {
            var result = await sender.Send(new GetInventoryMovementDashboardQuery(from, to, granularity, sku));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, result.Reserved, result.ReservationFailed, result.Released, result.Replenished });
        });

        group.MapGet("/dashboards/catalog-pricing", async (ISender sender, DateTimeOffset from, DateTimeOffset to, Granularity granularity = Granularity.Day) =>
        {
            var result = await sender.Send(new GetCatalogPricingDashboardQuery(from, to, granularity));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, result.ProductsCreated, result.PriceChanges, result.CategoryUpdates });
        });

        group.MapGet("/dashboards/promotions-effectiveness", async (ISender sender, DateTimeOffset from, DateTimeOffset to, Granularity granularity = Granularity.Day) =>
        {
            var result = await sender.Send(new GetPromotionsEffectivenessDashboardQuery(from, to, granularity));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, result.CouponsRedeemed, result.QuotesIssued, attributableOrderVolume = result.AttributableOrderVolume, result.RedemptionRate });
        });

        group.MapGet("/dashboards/user-growth", async (ISender sender, DateTimeOffset from, DateTimeOffset to, Granularity granularity = Granularity.Day) =>
        {
            var result = await sender.Send(new GetUserGrowthDashboardQuery(from, to, granularity));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, result.Signups, result.SessionsCreated, result.ProfileChanges });
        });

        group.MapGet("/dashboards/reviews-ratings", async (ISender sender, DateTimeOffset from, DateTimeOffset to, Granularity granularity = Granularity.Day) =>
        {
            var result = await sender.Send(new GetReviewsRatingsDashboardQuery(from, to, granularity));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, result.ReviewCount, ratingDistribution = result.RatingDistribution });
        });

        group.MapGet("/dashboards/admin-audit", async (ISender sender, DateTimeOffset from, DateTimeOffset to, string? actionType) =>
        {
            var result = await sender.Send(new GetAdminAuditDashboardQuery(from, to, actionType));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, result.Actions });
        });

        group.MapGet("/dashboards/notification-delivery", async (ISender sender, DateTimeOffset from, DateTimeOffset to, Granularity granularity, string? channel) =>
        {
            var result = await sender.Send(new GetNotificationDeliveryDashboardQuery(from, to, granularity, channel));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, byChannel = result.ByChannel });
        });

        // Added this pass (11th endpoint) — genai-business-assistant-spec.md §10.3 /
        // database-design.md "product_performance_dashboard". No `granularity` query parameter,
        // unlike every dashboard above (see GetProductPerformanceDashboardQueryHandler's remarks).
        group.MapGet("/dashboards/product-performance", async (ISender sender, DateTimeOffset from, DateTimeOffset to, string metric, string? category, string direction = "desc", int limit = 10) =>
        {
            var result = await sender.Send(new GetProductPerformanceDashboardQuery(from, to, metric, category, direction, limit));
            return Results.Ok(new { result.Envelope.GeneratedAt, result.Envelope.IsProvisional, result.Envelope.ReconciledThrough, products = result.Products });
        });
    }
}
