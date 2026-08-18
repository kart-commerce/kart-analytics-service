using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetCatalogPricingDashboard;

/// <summary>api-contract.yaml `GET /internal/v1/dashboards/catalog-pricing`.</summary>
public sealed record GetCatalogPricingDashboardQuery(DateTimeOffset From, DateTimeOffset To, Granularity Granularity) : IRequest<CatalogPricingDashboardResult>;

public sealed record CatalogPricingDashboardResult(DashboardEnvelope Envelope, long ProductsCreated, long PriceChanges, long CategoryUpdates);
