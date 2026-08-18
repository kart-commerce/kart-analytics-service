namespace Kart.Analytics.Application.Common.Interfaces;

/// <summary>
/// The one read/write path every dashboard/funnel projector and query handler uses against the
/// ten MongoDB collections. Deliberately generic — unlike the four real domain aggregates (each
/// with their own named repository, never a generic one), the ten Mongo collections are CQRS
/// projections, not aggregates (ddd-model.md: "read models are explicitly NOT modeled as
/// aggregates"), so a shared store here is the appropriate shape, not a primitive-obsession/DDD
/// violation. Technology-agnostic by design (no MongoDB type appears in this signature, and
/// <see cref="QueryAsync{TDocument}"/>'s filter delegate is expressed in plain LINQ against
/// <see cref="IQueryable{T}"/>) so Application stays free of an Infrastructure dependency;
/// `MongoReadModelStore` is Infrastructure's only implementation today.
/// </summary>
public interface IReadModelStore
{
    Task UpsertAsync<TDocument>(string collectionName, string documentId, TDocument document, CancellationToken cancellationToken) where TDocument : class;

    Task<List<TDocument>> QueryAsync<TDocument>(string collectionName, Func<IQueryable<TDocument>, IQueryable<TDocument>> query, CancellationToken cancellationToken) where TDocument : class;

    Task<TDocument?> FindByIdAsync<TDocument>(string collectionName, string documentId, CancellationToken cancellationToken) where TDocument : class;
}
