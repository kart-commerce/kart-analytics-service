namespace Kart.Analytics.Infrastructure.Security;

/// <summary>This service is a token *consumer*, never an issuer (BRD §24 — Identity Service is the
/// platform's single issuer). <see cref="JwksUri"/> points at Identity's `/.well-known/jwks.json`.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "kart-identity-service";

    public string JwksUri { get; set; } = "http://kart-identity-service/.well-known/jwks.json";
}
