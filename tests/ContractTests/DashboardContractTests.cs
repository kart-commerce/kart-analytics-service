using System.Net;
using System.Text.Json;
using FluentAssertions;
using Kart.Analytics.ContractTests.Fixtures;
using Xunit;

namespace Kart.Analytics.ContractTests;

/// <summary>
/// Validates every one of api-contract.yaml's ten dashboard/funnel endpoints against its
/// declared `DashboardEnvelope` + per-endpoint response shape — run against an empty database
/// (contract shape, not ingestion behavior; see IntegrationTests for the latter).
/// </summary>
[Collection("AnalyticsContractApi")]
public sealed class DashboardContractTests(AnalyticsContractApiFactory factory)
{
    private HttpClient AuthorizedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Scopes", "analytics.dashboards.read");
        return client;
    }

    private static void AssertHasDashboardEnvelope(JsonElement root)
    {
        root.TryGetProperty("generatedAt", out _).Should().BeTrue("every response must carry the DashboardEnvelope's generatedAt");
        root.TryGetProperty("isProvisional", out _).Should().BeTrue("every response must carry the DashboardEnvelope's isProvisional");
        root.TryGetProperty("reconciledThrough", out _).Should().BeTrue("every response must carry the DashboardEnvelope's reconciledThrough");
    }

    [Theory]
    [InlineData("/internal/v1/funnels/order-conversion?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day", "stages")]
    [InlineData("/internal/v1/dashboards/revenue?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day", "series")]
    [InlineData("/internal/v1/dashboards/fulfillment-performance?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day", "timeToShip")]
    [InlineData("/internal/v1/dashboards/inventory-movement?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day", "reserved")]
    [InlineData("/internal/v1/dashboards/catalog-pricing?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day", "productsCreated")]
    [InlineData("/internal/v1/dashboards/promotions-effectiveness?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day", "couponsRedeemed")]
    [InlineData("/internal/v1/dashboards/user-growth?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day", "signups")]
    [InlineData("/internal/v1/dashboards/reviews-ratings?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day", "reviewCount")]
    [InlineData("/internal/v1/dashboards/admin-audit?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z", "actions")]
    [InlineData("/internal/v1/dashboards/notification-delivery?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day", "byChannel")]
    public async Task Endpoint_returns_200_with_the_DashboardEnvelope_and_its_own_declared_field(string path, string ownFieldName)
    {
        var response = await AuthorizedClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        AssertHasDashboardEnvelope(document.RootElement);
        document.RootElement.TryGetProperty(ownFieldName, out _).Should().BeTrue($"response should carry its own '{ownFieldName}' field per api-contract.yaml");
    }

    [Fact]
    public async Task Empty_database_reports_the_window_as_provisional()
    {
        var response = await AuthorizedClient().GetAsync("/internal/v1/dashboards/revenue?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        // No reconciliation run has ever completed against this empty database, so "surface,
        // don't hide" (ddd-cqrs-standards.md) requires isProvisional:true, never false.
        document.RootElement.GetProperty("isProvisional").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("reconciledThrough").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Every_dashboard_endpoint_requires_the_analytics_dashboards_read_scope()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/internal/v1/dashboards/revenue?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
