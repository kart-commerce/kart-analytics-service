using Kart.Analytics.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Analytics.Infrastructure.Persistence.Converters;

/// <summary>Converters for this domain's non-Guid-backed value objects.</summary>
internal static class DomainValueConverters
{
    public static readonly ValueConverter<RunDate, DateOnly> RunDate = new(
        runDate => runDate.Value,
        value => Domain.ValueObjects.RunDate.From(value));
}
