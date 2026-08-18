using FluentAssertions;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Features.IngestEvent;
using Kart.Analytics.Application.Features.RedactUserPii;
using Kart.Analytics.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kart.Analytics.UnitTests.Features;

public class IngestEventCommandHandlerTests
{
    private readonly IIngestedEventRepository _repository = Substitute.For<IIngestedEventRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IngestEventCommandHandler _handler;

    public IngestEventCommandHandlerTests()
    {
        _clock.UtcNow.Returns(DateTimeOffset.Parse("2026-08-18T00:00:00Z"));
        _repository.UpsertAsync(Arg.Any<IngestedEvent>(), Arg.Any<CancellationToken>()).Returns(true);
        _handler = new IngestEventCommandHandler(_repository, _clock, _sender, Substitute.For<ILogger<IngestEventCommandHandler>>());
    }

    [Fact]
    public async Task Handle_upserts_the_event_and_reports_fresh_insert()
    {
        var command = new IngestEventCommand(Guid.NewGuid(), "OrderCreated", "kart-order-service", "order-1", DateTimeOffset.UtcNow, "{\"total\":10}");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.WasFreshInsert.Should().BeTrue();
        await _repository.Received(1).UpsertAsync(Arg.Is<IngestedEvent>(e => e.EventId.Value == command.EventId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_classifies_OrderCreated_as_containing_pii()
    {
        var command = new IngestEventCommand(Guid.NewGuid(), "OrderCreated", "kart-order-service", "order-1", DateTimeOffset.UtcNow, "{\"userId\":\"u1\"}");

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).UpsertAsync(Arg.Is<IngestedEvent>(e => e.ContainsPii), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_does_not_classify_ProductPriceChanged_as_containing_pii()
    {
        var command = new IngestEventCommand(Guid.NewGuid(), "ProductPriceChanged", "kart-product-service", "sku-1", DateTimeOffset.UtcNow, "{\"sku\":\"sku-1\"}");

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).UpsertAsync(Arg.Is<IngestedEvent>(e => !e.ContainsPii), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_triggers_redaction_when_event_type_is_UserDataErased()
    {
        var eventId = Guid.NewGuid();
        var command = new IngestEventCommand(eventId, "UserDataErased", "kart-user-service", "user-1", DateTimeOffset.UtcNow, "{\"userId\":\"user-1\",\"erasedAt\":\"2026-08-18T00:00:00Z\"}");

        await _handler.Handle(command, CancellationToken.None);

        await _sender.Received(1).Send(Arg.Is<RedactUserPiiCommand>(c => c.UserId == "user-1" && c.TriggeringEventId == eventId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_does_not_trigger_redaction_for_other_event_types()
    {
        var command = new IngestEventCommand(Guid.NewGuid(), "OrderCreated", "kart-order-service", "order-1", DateTimeOffset.UtcNow, "{\"userId\":\"user-1\"}");

        await _handler.Handle(command, CancellationToken.None);

        await _sender.DidNotReceive().Send(Arg.Any<RedactUserPiiCommand>(), Arg.Any<CancellationToken>());
    }
}
