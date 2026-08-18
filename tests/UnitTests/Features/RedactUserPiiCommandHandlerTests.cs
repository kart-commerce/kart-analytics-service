using FluentAssertions;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Features.RedactUserPii;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using DomainEventId = Kart.Analytics.Domain.ValueObjects.EventId;

namespace Kart.Analytics.UnitTests.Features;

public class RedactUserPiiCommandHandlerTests
{
    private readonly IIngestedEventRepository _ingestedEventRepository = Substitute.For<IIngestedEventRepository>();
    private readonly IPiiRedactionRecordRepository _redactionRecordRepository = Substitute.For<IPiiRedactionRecordRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly RedactUserPiiCommandHandler _handler;

    public RedactUserPiiCommandHandlerTests()
    {
        _clock.UtcNow.Returns(DateTimeOffset.Parse("2026-08-18T00:00:00Z"));
        _handler = new RedactUserPiiCommandHandler(_ingestedEventRepository, _redactionRecordRepository, _clock, Substitute.For<ILogger<RedactUserPiiCommandHandler>>());
    }

    [Fact]
    public async Task Handle_redacts_every_pending_row_and_writes_one_immutable_record()
    {
        var envelope = EventEnvelope.Create("UserRegistered", "kart-identity-service", "user-1", DateTimeOffset.UtcNow);
        var schema = SchemaVersionPointer.Create("abc", "1.0");
        var row1 = IngestedEvent.Create(DomainEventId.New(), envelope, schema, "{\"userId\":\"user-1\",\"email\":\"a@b.com\"}", true, DateTimeOffset.UtcNow, "system:analytics-ingestion-consumer");
        var row2 = IngestedEvent.Create(DomainEventId.New(), envelope, schema, "{\"userId\":\"user-1\",\"email\":\"c@d.com\"}", true, DateTimeOffset.UtcNow, "system:analytics-ingestion-consumer");

        _ingestedEventRepository.GetPiiPendingForUserAsync("user-1", Arg.Any<CancellationToken>()).Returns([row1, row2]);

        var triggeringEventId = Guid.NewGuid();
        await _handler.Handle(new RedactUserPiiCommand("user-1", triggeringEventId), CancellationToken.None);

        row1.PiiRedactedAt.Should().NotBeNull();
        row2.PiiRedactedAt.Should().NotBeNull();
        row1.Payload.Should().NotContain("a@b.com");

        await _ingestedEventRepository.Received(1).SaveRedactedBatchAsync(
            Arg.Is<IReadOnlyList<IngestedEvent>>(list => list.Count == 2), Arg.Any<CancellationToken>());

        await _redactionRecordRepository.Received(1).AddAsync(
            Arg.Is<PiiRedactionRecord>(r => r.UserId == "user-1" && r.RowsRedacted == 2 && r.TriggeringEventId.Value == triggeringEventId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_writes_a_zero_row_record_when_nothing_is_pending()
    {
        _ingestedEventRepository.GetPiiPendingForUserAsync("user-2", Arg.Any<CancellationToken>()).Returns([]);

        await _handler.Handle(new RedactUserPiiCommand("user-2", Guid.NewGuid()), CancellationToken.None);

        await _ingestedEventRepository.DidNotReceive().SaveRedactedBatchAsync(Arg.Any<IReadOnlyList<IngestedEvent>>(), Arg.Any<CancellationToken>());
        await _redactionRecordRepository.Received(1).AddAsync(Arg.Is<PiiRedactionRecord>(r => r.RowsRedacted == 0), Arg.Any<CancellationToken>());
    }
}
