using FluentAssertions;
using Kart.Analytics.Application.Common;

namespace Kart.Analytics.UnitTests.Common;

public class PiiRedactorTests
{
    [Fact]
    public void Redact_nulls_known_pii_fields_but_preserves_everything_else()
    {
        const string payload = """{"userId":"user-1","email":"a@b.com","displayName":"Alice","orderId":"order-1"}""";

        var redacted = PiiRedactor.Redact(payload);

        redacted.Should().Contain("\"userId\":\"user-1\"").And.Contain("\"orderId\":\"order-1\"");
        redacted.Should().NotContain("a@b.com").And.NotContain("Alice");
    }

    [Fact]
    public void Redact_is_a_no_op_when_no_known_pii_field_is_present()
    {
        const string payload = """{"orderId":"order-1","total":10}""";

        var redacted = PiiRedactor.Redact(payload);

        redacted.Should().Contain("\"orderId\":\"order-1\"").And.Contain("\"total\":10");
    }
}
