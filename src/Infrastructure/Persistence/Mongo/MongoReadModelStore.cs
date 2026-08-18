using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Application.Common.Interfaces;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Kart.Analytics.Infrastructure.Persistence.Mongo;

public sealed class MongoReadModelStore(MongoContext context) : IReadModelStore
{
    /// <summary>
    /// Upserting into a SHARDED collection requires the query filter to include an equality
    /// match on the full shard key — `{granularity, bucketStart}` per `scripts/init-mongo-cluster.sh`
    /// — or the server rejects it with "could not extract exact shard key" (real error, only
    /// reproducible against a genuinely sharded cluster, not the single-node `mongo:7` the
    /// automated test suite uses). Filtering on `_id` alone, which is what every one of the nine
    /// bucketed collections' documents already encode the shard key into, is not sufficient by
    /// itself for the server to route the upsert.
    /// </summary>
    public async Task UpsertAsync<TDocument>(string collectionName, string documentId, TDocument document, CancellationToken cancellationToken) where TDocument : class
    {
        var collection = context.Database.GetCollection<TDocument>(collectionName);

        var filter = document is BucketedReadModelBase bucketed
            ? Builders<TDocument>.Filter.And(
                Builders<TDocument>.Filter.Eq("_id", documentId),
                Builders<TDocument>.Filter.Eq("granularity", bucketed.Granularity),
                Builders<TDocument>.Filter.Eq("bucketStart", bucketed.BucketStart))
            : Builders<TDocument>.Filter.Eq("_id", documentId);

        await collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<List<TDocument>> QueryAsync<TDocument>(string collectionName, Func<IQueryable<TDocument>, IQueryable<TDocument>> query, CancellationToken cancellationToken) where TDocument : class
    {
        var collection = context.Database.GetCollection<TDocument>(collectionName);
        var queryable = query(collection.AsQueryable());

        // The delegate's declared return type is the technology-agnostic IQueryable<TDocument>
        // (so Application-layer query handlers never reference an MongoDB.Driver type), but the
        // Mongo LINQ provider preserves IMongoQueryable<TDocument> through Where/OrderBy at
        // runtime — this cast is safe and is what makes the async, server-side-executed
        // ToListAsync available.
        return await ((IMongoQueryable<TDocument>)queryable).ToListAsync(cancellationToken);
    }

    public async Task<TDocument?> FindByIdAsync<TDocument>(string collectionName, string documentId, CancellationToken cancellationToken) where TDocument : class
    {
        var collection = context.Database.GetCollection<TDocument>(collectionName);
        var filter = Builders<TDocument>.Filter.Eq("_id", documentId);
        return await collection.Find(filter).SingleOrDefaultAsync(cancellationToken);
    }
}
