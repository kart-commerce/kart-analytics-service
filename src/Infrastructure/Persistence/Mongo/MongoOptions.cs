using System.ComponentModel.DataAnnotations;

namespace Kart.Analytics.Infrastructure.Persistence.Mongo;

/// <summary>
/// database-design.md's MongoDB read model connection. Points at the `mongos` router of the
/// sharded cluster (config-server + 2 shard replsets + mongos — mirroring kart-product-service's
/// local topology), not a single `mongod` instance, so this service's own connection code is
/// identical whether the cluster behind it is sharded or not.
/// </summary>
public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    [Required]
    public string ConnectionString { get; init; } = "mongodb://localhost:27017";

    [Required]
    public string Database { get; init; } = "kart_analytics";
}
