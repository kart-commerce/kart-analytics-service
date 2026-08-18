using Kart.Analytics.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Analytics.Infrastructure.Persistence.Converters;

/// <summary>
/// One generic <see cref="ValueConverter{TModel,TProvider}"/> factory for every Guid-backed
/// strongly-typed ID in the domain (<see cref="ITypedEntityId{TSelf}"/>), instead of a
/// hand-written converter per ID type — mirrors kart-identity-service's
/// <c>TypedIdValueConverters</c> exactly.
/// </summary>
internal static class TypedIdValueConverters
{
    public static ValueConverter<TId, Guid> For<TId>() where TId : struct, ITypedEntityId<TId>
    {
        // TId.From is a static abstract interface member — referencing it directly inside the
        // conversion lambda would put an unsupported construct into the expression tree
        // ValueConverter compiles that lambda into (CS8927). Capturing it as an ordinary delegate
        // first keeps the expression tree free of the abstract-member reference itself.
        Func<Guid, TId> fromGuid = TId.From;
        return new ValueConverter<TId, Guid>(id => id.Value, value => fromGuid(value));
    }
}
