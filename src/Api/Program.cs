using Kart.Analytics.Api;
using Kart.Analytics.Api.Endpoints;
using Kart.Analytics.Api.HealthChecks;
using Kart.Analytics.Application;
using Kart.Analytics.Infrastructure;
using Kart.Analytics.Infrastructure.Security;
using Kart.Shared.Auditing;
using Kart.Shared.Configuration;
using Kart.Shared.ErrorHandling;
using Kart.Shared.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service - never reimplemented per service.
builder.AddKartGlobalConfig("kart-analytics-service");

// kart-conventions.md Observability section: Serilog + OpenTelemetry SDK behind one DI call.
// STANDARD (not 100%) trace-sampling tier - Analytics is a pure consumer sink, never an Order
// Saga participant (design-decisions.md "Observability & Instrumentation").
builder.AddKartObservability("kart-analytics-service");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// api-contract.yaml: OAuth2 Client Credentials, Identity is the platform's single token issuer.
// This service only validates via Identity's JWKS endpoint, gated on the single
// `analytics.dashboards.read` scope - no public Gateway route exists for this API at all
// (requirement-spec.md §1), so this is the only gate every request passes through.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwksSigningKeyResolver, IOptions<JwtOptions>>((options, resolver, jwtOptions) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Value.Issuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, kid, _) => resolver.ResolveSigningKeys(kid),
        };
    });
builder.Services.AddAuthorization(options => options.AddPolicy("AnalyticsDashboardsRead", policy =>
    policy.RequireAssertion(context =>
        context.User.FindAll("scope").Any(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("analytics.dashboards.read"))
        || context.User.FindAll("scopes").Any(c => c.Value == "analytics.dashboards.read"))));

// kart-conventions.md Error Handling section: the single global exception handler + ProblemDetails
// factory, wired once via the shared package - no local try/catch for translation anywhere in
// this service's handler/endpoint code.
builder.Services.AddKartErrorHandling();

// This service's own explicit BRD §24.3 audit-trail requirement is wired inside AddInfrastructure
// (AddKartAuditing<PostgresAuditLogWriter>), not the Kart.Shared.Auditing NullAuditLogWriter default.

builder.Services.AddHealthChecks()
    .AddCheck<AnalyticsDbHealthCheck>("analytics-db", tags: ["ready"])
    .AddCheck<MongoHealthCheck>("mongo", tags: ["ready"])
    .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);

var app = builder.Build();

await StartupConnectivityChecks.RunAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Per-HTTP-request Information log (method/path/status/elapsed) - registered outermost, wrapping
// UseKartErrorHandling below, so this always logs the *final* status code a client actually received.
app.UseSerilogRequestLogging();

// The single global error handler - every unhandled exception is translated to the platform's
// ProblemDetails envelope and logged here, so no endpoint needs its own try/catch.
app.UseKartErrorHandling();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Prometheus scrape target (observability-standards.md's mandatory /metrics).
app.MapPrometheusScrapingEndpoint();

app.MapDashboardEndpoints();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Exposed for WebApplicationFactory<Program> in IntegrationTests/ContractTests.
public partial class Program;
