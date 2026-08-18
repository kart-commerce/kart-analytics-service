using System.Net;
using FluentAssertions;
using Kart.Analytics.IntegrationTests.Fixtures;
using Xunit;

namespace Kart.Analytics.IntegrationTests;

/// <summary>api-contract.yaml: every dashboard/funnel endpoint is gated by the
/// `analytics.dashboards.read` scope — no public Gateway route exists at all.</summary>
[Collection("AnalyticsApi")]
public sealed class DashboardEndpointAuthorizationTests(AnalyticsApiFactory factory)
{
    [Fact]
    public async Task Request_without_a_token_is_rejected_as_unauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/internal/v1/dashboards/revenue?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_with_a_token_missing_the_required_scope_is_forbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Scopes", "some.other.scope");

        var response = await client.GetAsync("/internal/v1/dashboards/revenue?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Request_with_the_required_scope_succeeds()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Scopes", "analytics.dashboards.read");

        var response = await client.GetAsync("/internal/v1/dashboards/revenue?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&granularity=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Product_performance_endpoint_also_requires_the_analytics_dashboards_read_scope()
    {
        // Mirrors the three revenue-endpoint checks above, for the 11th dashboard endpoint
        // (added this pass) — confirms it sits in the same MapGroup(...).RequireAuthorization(...)
        // as every other dashboard, not a separately-configured route.
        var client = factory.CreateClient();

        var unauthorized = await client.GetAsync("/internal/v1/dashboards/product-performance?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&metric=revenue");
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Add("X-Test-Scopes", "some.other.scope");
        var forbidden = await client.GetAsync("/internal/v1/dashboards/product-performance?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&metric=revenue");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        client.DefaultRequestHeaders.Remove("X-Test-Scopes");
        client.DefaultRequestHeaders.Add("X-Test-Scopes", "analytics.dashboards.read");
        var ok = await client.GetAsync("/internal/v1/dashboards/product-performance?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z&metric=revenue");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_live_and_ready_endpoints_are_reachable_without_auth()
    {
        var client = factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
