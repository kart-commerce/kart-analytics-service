using FluentAssertions;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Application.Features.GetProductPerformanceDashboard;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;
using NSubstitute;

namespace Kart.Analytics.UnitTests.Features.GetProductPerformanceDashboard;

/// <summary>
/// Focused coverage for the one behavior this feature adds beyond "make the endpoint work":
/// aggregating `OrderCreated.items` into one document per `(granularity, bucketStart, sku)`
/// (database-design.md "product_performance_dashboard"). Ranking/tiebreak determinism is covered
/// separately by <see cref="GetProductPerformanceDashboardQueryHandlerTests"/>.
/// </summary>
public sealed class ProductPerformanceDashboardProjectorTests
{
    private readonly IIngestedEventRepository _ingestedEventRepository = Substitute.For<IIngestedEventRepository>();
    private readonly IReadModelStore _readModelStore = Substitute.For<IReadModelStore>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ProductPerformanceDashboardProjector _projector;

    private static readonly DateTimeOffset WindowFrom = DateTimeOffset.Parse("2026-08-17T00:00:00Z");
    private static readonly DateTimeOffset WindowTo = DateTimeOffset.Parse("2026-08-18T00:00:00Z");

    public ProductPerformanceDashboardProjectorTests()
    {
        _clock.UtcNow.Returns(DateTimeOffset.Parse("2026-08-18T01:00:00Z"));
        _projector = new ProductPerformanceDashboardProjector(_ingestedEventRepository, _readModelStore, _clock);
    }

    private static IngestedEvent CreateOrderCreatedEvent(string orderId, string itemsJson)
    {
        var envelope = EventEnvelope.Create("OrderCreated", "kart-order-service", orderId, WindowFrom.AddHours(1));
        var schemaVersion = SchemaVersionPointer.Create("order-created-v1", "1.0");
        var payload = $$"""{"orderId":"{{orderId}}","userId":"user-1","items":{{itemsJson}},"total":0}""";
        return IngestedEvent.Create(EventId.New(), envelope, schemaVersion, payload, containsPii: true, WindowFrom, "system:analytics-ingestion-consumer");
    }

    [Fact]
    public async Task RecomputeAsync_aggregates_revenue_units_and_order_count_per_sku()
    {
        var order1 = CreateOrderCreatedEvent(
            "order-1",
            """[{"sku":"SKU-A","qty":2,"unitPrice":{"amount":10.00,"currency":"USD"}},{"sku":"SKU-B","qty":1,"unitPrice":{"amount":5.00,"currency":"USD"}}]""");
        var order2 = CreateOrderCreatedEvent(
            "order-2",
            """[{"sku":"SKU-A","qty":1,"unitPrice":{"amount":10.00,"currency":"USD"}}]""");

        _ingestedEventRepository.GetByTypeInWindowAsync("OrderCreated", WindowFrom, WindowTo, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<IngestedEvent>>([order1, order2]);

        await _projector.RecomputeAsync(WindowFrom, WindowTo, Granularity.Day, isProvisional: true, reconciledThrough: null, CancellationToken.None);

        // SKU-A: 2 units @ $10 (order-1) + 1 unit @ $10 (order-2) = $30 revenue, 3 units, seen in 2 distinct orders.
        await _readModelStore.Received(1).UpsertAsync(
            "product_performance_dashboard",
            Arg.Any<string>(),
            Arg.Is<ProductPerformanceReadModel>(d =>
                d.Sku == "SKU-A" && d.RevenueAmount == 30.00m && d.RevenueCurrency == "USD" && d.UnitsSold == 3 && d.OrderCount == 2),
            Arg.Any<CancellationToken>());

        // SKU-B: 1 unit @ $5, seen in 1 order.
        await _readModelStore.Received(1).UpsertAsync(
            "product_performance_dashboard",
            Arg.Any<string>(),
            Arg.Is<ProductPerformanceReadModel>(d =>
                d.Sku == "SKU-B" && d.RevenueAmount == 5.00m && d.RevenueCurrency == "USD" && d.UnitsSold == 1 && d.OrderCount == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecomputeAsync_counts_an_order_once_per_sku_even_if_the_sku_appears_in_two_line_items()
    {
        var order = CreateOrderCreatedEvent(
            "order-1",
            """[{"sku":"SKU-A","qty":1,"unitPrice":{"amount":10.00,"currency":"USD"}},{"sku":"SKU-A","qty":1,"unitPrice":{"amount":10.00,"currency":"USD"}}]""");

        _ingestedEventRepository.GetByTypeInWindowAsync("OrderCreated", WindowFrom, WindowTo, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<IngestedEvent>>([order]);

        await _projector.RecomputeAsync(WindowFrom, WindowTo, Granularity.Day, isProvisional: true, reconciledThrough: null, CancellationToken.None);

        await _readModelStore.Received(1).UpsertAsync(
            "product_performance_dashboard",
            Arg.Any<string>(),
            Arg.Is<ProductPerformanceReadModel>(d => d.Sku == "SKU-A" && d.UnitsSold == 2 && d.OrderCount == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecomputeAsync_writes_no_documents_when_no_orders_are_in_window()
    {
        _ingestedEventRepository.GetByTypeInWindowAsync("OrderCreated", WindowFrom, WindowTo, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<IngestedEvent>>([]);

        await _projector.RecomputeAsync(WindowFrom, WindowTo, Granularity.Day, isProvisional: true, reconciledThrough: null, CancellationToken.None);

        await _readModelStore.DidNotReceive().UpsertAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ProductPerformanceReadModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecomputeAsync_skips_a_line_item_with_no_sku_without_failing_the_whole_recompute()
    {
        var order = CreateOrderCreatedEvent(
            "order-1",
            """[{"qty":1,"unitPrice":{"amount":10.00,"currency":"USD"}},{"sku":"SKU-A","qty":1,"unitPrice":{"amount":10.00,"currency":"USD"}}]""");

        _ingestedEventRepository.GetByTypeInWindowAsync("OrderCreated", WindowFrom, WindowTo, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<IngestedEvent>>([order]);

        await _projector.RecomputeAsync(WindowFrom, WindowTo, Granularity.Day, isProvisional: true, reconciledThrough: null, CancellationToken.None);

        await _readModelStore.Received(1).UpsertAsync(
            "product_performance_dashboard",
            Arg.Any<string>(),
            Arg.Is<ProductPerformanceReadModel>(d => d.Sku == "SKU-A"),
            Arg.Any<CancellationToken>());
    }
}
