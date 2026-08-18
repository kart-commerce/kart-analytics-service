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
    public async Task Health_live_and_ready_endpoints_are_reachable_without_auth()
    {
        var client = factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
