using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Kart.Analytics.Infrastructure.Persistence.Mongo;

/// <summary>
/// database-design.md's ten dashboard/funnel collections. Every write/read goes through
/// <see cref="IReadModelStore"/> (generic over the collection name + document type — the
/// collections are CQRS projections, not aggregates, so a shared accessor is the correct shape
/// here, unlike the four real domain aggregates' own named repositories).
/// </summary>
public sealed class MongoContext
{
    public const string OrderConversionFunnel = "order_conversion_funnel";
    public const string RevenueDashboard = "revenue_dashboard";
    public const string FulfillmentPerformanceDashboard = "fulfillment_performance_dashboard";
    public const string InventoryMovementDashboard = "inventory_movement_dashboard";
    public const string CatalogPricingDashboard = "catalog_pricing_dashboard";
    public const string PromotionsEffectivenessDashboard = "promotions_effectiveness_dashboard";
    public const string UserGrowthDashboard = "user_growth_dashboard";
    public const string ReviewsRatingsDashboard = "reviews_ratings_dashboard";
    public const string AdminAuditLog = "admin_audit_log";
    public const string NotificationDeliveryDashboard = "notification_delivery_dashboard";

    /// <summary>Every collection name, for the index initializer / health check / cluster init script to iterate.</summary>
    public static readonly string[] AllCollectionNames =
    [
        OrderConversionFunnel, RevenueDashboard, FulfillmentPerformanceDashboard, InventoryMovementDashboard,
        CatalogPricingDashboard, PromotionsEffectivenessDashboard, UserGrowthDashboard, ReviewsRatingsDashboard,
        AdminAuditLog, NotificationDeliveryDashboard,
    ];

    public IMongoDatabase Database { get; }

    static MongoContext()
    {
        // database-design.md's documents use camelCase field names (`granularity`, `bucketStart`,
        // `isProvisional`, ...) while this codebase's C# read-model classes stay PascalCase by
        // convention — one global convention pack bridges the two instead of a per-property
        // [BsonElement("...")] attribute on every field of every one of the ten documents.
        ConventionRegistry.Register(
            "kart-analytics-camelCase",
            new ConventionPack { new CamelCaseElementNameConvention() },
            _ => true);
    }

    public MongoContext(IOptions<MongoOptions> options)
    {
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        Database = client.GetDatabase(options.Value.Database);
    }
}
