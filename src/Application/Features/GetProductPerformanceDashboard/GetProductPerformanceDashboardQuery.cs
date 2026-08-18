using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetProductPerformanceDashboard;

/// <summary>
/// api-contract.yaml `GET /internal/v1/dashboards/product-performance`. Unlike every other
/// bucketed dashboard, this endpoint has no `granularity` query parameter — it ranks products
/// over an arbitrary `from`/`to` window, not a caller-chosen bucket size (see
/// `GetProductPerformanceDashboardQueryHandler`'s remarks for how the underlying bucketed read
/// model is queried anyway). `Metric` and `Direction` are plain strings, not enums: the wire
/// values (`revenue`/`units_sold`/`order_count`, `desc`/`asc`) are snake_case/lowercase and don't
/// round-trip through ASP.NET Core's default by-name enum query-string binding, so they are
/// validated against the allowed set by <see cref="GetProductPerformanceDashboardQueryValidator"/>
/// instead — the same treatment this service already gives every other free-text filter
/// (`sku`, `category`, `channel`, `actionType`).
/// </summary>
public sealed record GetProductPerformanceDashboardQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    string Metric,
    string? Category,
    string Direction,
    int Limit) : IRequest<ProductPerformanceDashboardResult>;

public sealed record ProductPerformanceDashboardResult(DashboardEnvelope Envelope, IReadOnlyList<ProductPerformanceEntry> Products);

/// <summary>One ranked product — always carries all three metrics (revenue, unitsSold,
/// orderCount) regardless of which one <see cref="GetProductPerformanceDashboardQuery.Metric"/>
/// ranks by, so a caller can show secondary metrics alongside the ranked one
/// (genai-business-assistant-spec.md §10.3).</summary>
public sealed record ProductPerformanceEntry(string Sku, string? Category, Money Revenue, long UnitsSold, long OrderCount);
