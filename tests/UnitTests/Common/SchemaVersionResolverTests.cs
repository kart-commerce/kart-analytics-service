using FluentAssertions;
using Kart.Analytics.Application.Common.SchemaVersioning;

namespace Kart.Analytics.UnitTests.Common;

public class SchemaVersionResolverTests
{
    [Fact]
    public void Resolve_produces_the_same_schema_id_for_the_same_shape()
    {
        var a = SchemaVersionResolver.Resolve("""{"orderId":"1","userId":"u1","total":10}""");
        var b = SchemaVersionResolver.Resolve("""{"orderId":"2","userId":"u2","total":20}""");

        a.SchemaId.Should().Be(b.SchemaId);
    }

    [Fact]
    public void Resolve_produces_a_different_schema_id_when_the_shape_changes()
    {
        var original = SchemaVersionResolver.Resolve("""{"orderId":"1","userId":"u1"}""");
        var withNewField = SchemaVersionResolver.Resolve("""{"orderId":"1","userId":"u1","promoCode":"X"}""");

        original.SchemaId.Should().NotBe(withNewField.SchemaId);
    }

    [Fact]
    public void Resolve_is_insensitive_to_property_ordering()
    {
        var a = SchemaVersionResolver.Resolve("""{"a":1,"b":2}""");
        var b = SchemaVersionResolver.Resolve("""{"b":2,"a":1}""");

        a.SchemaId.Should().Be(b.SchemaId);
    }
}
