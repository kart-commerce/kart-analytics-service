using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Application.Features.GetAdminAuditDashboard;
using Kart.Analytics.Application.Features.GetInventoryMovementDashboard;
using Kart.Analytics.Application.Features.GetNotificationDeliveryDashboard;
using Kart.Analytics.Application.Features.GetRevenueDashboard;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Kart.Analytics.Infrastructure.Persistence.Mongo;

/// <summary>
/// The Mongo-only equivalent of an EF Core migration (kart-recommendation-service's own
/// convention) — database-design.md's Indexing Rationale section, restated here as the actual
/// index declarations. Fire-and-forget on startup: a Mongo outage must not block Kestrel from
/// starting. Sharding itself (`sh.enableSharding`/`sh.shardCollection` on `{granularity,
/// bucketStart}`) is a one-time cluster-admin operation performed by
/// `scripts/init-mongo-cluster.sh`, not repeated here on every app boot — mirrors
/// kart-product-service's split between this hosted service's per-collection secondary indexes
/// and its own cluster init script's sharding commands.
/// </summary>
public sealed class MongoIndexInitializerHostedService(MongoContext context, ILogger<MongoIndexInitializerHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = DeclareIndexesAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task DeclareIndexesAsync(CancellationToken cancellationToken)
    {
        // {granularity, bucketStart} on every time-bucketed collection — direct 1:1 match to
        // every `GET .../{dashboard}?from=&to=&granularity=` query.
        foreach (var collectionName in MongoContext.AllCollectionNames.Where(n => n != MongoContext.AdminAuditLog))
        {
            await CreateBucketIndexAsync(collectionName, cancellationToken);
        }

        // Additional sku/category/channel compound prefix — the optional filter params those
        // three endpoints expose.
        await CreateIndexAsync($"{MongoContext.RevenueDashboard}.(granularity,bucketStart,sku,category)", () =>
            context.Database.GetCollection<RevenueDashboardReadModel>(MongoContext.RevenueDashboard).Indexes.CreateOneAsync(
                new CreateIndexModel<RevenueDashboardReadModel>(Builders<RevenueDashboardReadModel>.IndexKeys
                    .Ascending(d => d.Granularity).Ascending(d => d.BucketStart).Ascending(d => d.Sku).Ascending(d => d.Category)),
                cancellationToken: cancellationToken));

        await CreateIndexAsync($"{MongoContext.InventoryMovementDashboard}.(granularity,bucketStart,sku)", () =>
            context.Database.GetCollection<InventoryMovementDashboardReadModel>(MongoContext.InventoryMovementDashboard).Indexes.CreateOneAsync(
                new CreateIndexModel<InventoryMovementDashboardReadModel>(Builders<InventoryMovementDashboardReadModel>.IndexKeys
                    .Ascending(d => d.Granularity).Ascending(d => d.BucketStart).Ascending(d => d.Sku)),
                cancellationToken: cancellationToken));

        await CreateIndexAsync($"{MongoContext.NotificationDeliveryDashboard}.(granularity,bucketStart,channel)", () =>
            context.Database.GetCollection<NotificationDeliveryDashboardReadModel>(MongoContext.NotificationDeliveryDashboard).Indexes.CreateOneAsync(
                new CreateIndexModel<NotificationDeliveryDashboardReadModel>(Builders<NotificationDeliveryDashboardReadModel>.IndexKeys
                    .Ascending(d => d.Granularity).Ascending(d => d.BucketStart).Ascending(d => d.Channel)),
                cancellationToken: cancellationToken));

        // admin_audit_log — a log query, not a bucket aggregate (no granularity param).
        await CreateIndexAsync($"{MongoContext.AdminAuditLog}.(occurredAt,actionType,adminId)", () =>
            context.Database.GetCollection<AdminAuditLogReadModel>(MongoContext.AdminAuditLog).Indexes.CreateOneAsync(
                new CreateIndexModel<AdminAuditLogReadModel>(Builders<AdminAuditLogReadModel>.IndexKeys
                    .Ascending(d => d.OccurredAt).Ascending(d => d.ActionType).Ascending(d => d.AdminId)),
                cancellationToken: cancellationToken));
    }

    private Task CreateBucketIndexAsync(string collectionName, CancellationToken cancellationToken) =>
        CreateIndexAsync($"{collectionName}.(granularity,bucketStart)", () =>
            context.Database.GetCollection<BucketedReadModelBase>(collectionName).Indexes.CreateOneAsync(
                new CreateIndexModel<BucketedReadModelBase>(Builders<BucketedReadModelBase>.IndexKeys.Ascending(d => d.Granularity).Ascending(d => d.BucketStart)),
                cancellationToken: cancellationToken));

    private async Task CreateIndexAsync(string description, Func<Task<string>> createIndex)
    {
        try
        {
            await createIndex();
            logger.LogInformation("Declared MongoDB index '{Description}'", description);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not declare MongoDB index '{Description}' at startup", description);
        }
    }
}
