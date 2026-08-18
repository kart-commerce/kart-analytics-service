using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kart.Analytics.IntegrationTests.Fixtures;

/// <summary>
/// Header-driven fake authentication handler for integration/contract tests — the established
/// platform convention (kart-recommendation-service/kart-wishlist-service's own `TestAuthHandler`),
/// bypassing real JWKS/JWT validation entirely rather than minting a real signed token per test.
/// Reads `X-Test-Scopes` (space-separated, matching the real OAuth2 access token's own `scope`
/// claim shape) — no header at all means "unauthenticated," exercising the real 401 path; a
/// header present but missing `analytics.dashboards.read` exercises the real 403 path.
/// </summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Scopes", out var scopes))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new("sub", "test-client"), new("scope", scopes.ToString()) };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
