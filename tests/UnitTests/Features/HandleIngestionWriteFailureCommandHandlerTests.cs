using FluentAssertions;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Features.HandleIngestionWriteFailure;
using Kart.Analytics.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kart.Analytics.UnitTests.Features;

public class HandleIngestionWriteFailureCommandHandlerTests
{
    [Fact]
    public async Task Handle_parks_the_event_with_the_given_retry_count_and_reason()
    {
        var repository = Substitute.For<IDeadLetteredEventRepository>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.Parse("2026-08-18T00:00:00Z"));
        var handler = new HandleIngestionWriteFailureCommandHandler(repository, clock, Substitute.For<ILogger<HandleIngestionWriteFailureCommandHandler>>());

        var command = new HandleIngestionWriteFailureCommand(Guid.NewGuid(), "OrderCreated", "{}", "boom", 3);

        await handler.Handle(command, CancellationToken.None);

        await repository.Received(1).AddAsync(
            Arg.Is<DeadLetteredEvent>(e => e.EventId.Value == command.EventId && e.RetryCount == 3 && e.FailureReason == "boom"),
            Arg.Any<CancellationToken>());
    }
}
