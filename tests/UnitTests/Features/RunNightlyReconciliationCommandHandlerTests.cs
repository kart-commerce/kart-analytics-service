using FluentAssertions;
using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using Kart.Analytics.Application.Features.RunNightlyReconciliation;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.Enums;
using Kart.Analytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Kart.Analytics.UnitTests.Features;

public class RunNightlyReconciliationCommandHandlerTests
{
    private readonly IReconciliationRunRepository _runRepository = Substitute.For<IReconciliationRunRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private static readonly DateOnly TargetDate = new(2026, 8, 17);

    public RunNightlyReconciliationCommandHandlerTests()
    {
        _clock.UtcNow.Returns(DateTimeOffset.Parse("2026-08-18T01:00:00Z"));
    }

    private RunNightlyReconciliationCommandHandler CreateHandler(IEnumerable<IReadModelProjector> projectors) =>
        new(_runRepository, projectors, _clock, Substitute.For<ILogger<RunNightlyReconciliationCommandHandler>>());

    [Fact]
    public async Task Handle_is_a_no_op_when_todays_run_is_already_completed()
    {
        var existing = ReconciliationRun.StartNew(RunDate.From(TargetDate), _clock.UtcNow, "system:analytics-reconciliation-job");
        existing.Complete(_clock.UtcNow, "system:analytics-reconciliation-job");
        _runRepository.FindByRunDateAsync(Arg.Any<RunDate>(), Arg.Any<CancellationToken>()).Returns(existing);

        var projector = Substitute.For<IReadModelProjector>();
        var handler = CreateHandler([projector]);

        var result = await handler.Handle(new RunNightlyReconciliationCommand(TargetDate), CancellationToken.None);

        result.Outcome.Should().Be(ReconciliationOutcome.AlreadyCompleted);
        await projector.DidNotReceive().RecomputeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Granularity>(), Arg.Any<bool>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_skips_when_a_run_for_the_same_date_is_already_in_flight()
    {
        var existing = ReconciliationRun.StartNew(RunDate.From(TargetDate), _clock.UtcNow, "system:analytics-reconciliation-job");
        _runRepository.FindByRunDateAsync(Arg.Any<RunDate>(), Arg.Any<CancellationToken>()).Returns(existing);

        var handler = CreateHandler([]);
        var result = await handler.Handle(new RunNightlyReconciliationCommand(TargetDate), CancellationToken.None);

        result.Outcome.Should().Be(ReconciliationOutcome.SkippedAlreadyRunning);
        await _runRepository.DidNotReceive().AddAsync(Arg.Any<ReconciliationRun>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_recomputes_every_projector_across_all_four_granularities_and_completes()
    {
        _runRepository.FindByRunDateAsync(Arg.Any<RunDate>(), Arg.Any<CancellationToken>()).Returns((ReconciliationRun?)null);

        var projectorA = Substitute.For<IReadModelProjector>();
        var projectorB = Substitute.For<IReadModelProjector>();
        var handler = CreateHandler([projectorA, projectorB]);

        var result = await handler.Handle(new RunNightlyReconciliationCommand(TargetDate), CancellationToken.None);

        result.Outcome.Should().Be(ReconciliationOutcome.Completed);
        await _runRepository.Received(1).AddAsync(Arg.Any<ReconciliationRun>(), Arg.Any<CancellationToken>());

        // 4 granularities x 2 projectors = 8 calls each, all marked final (isProvisional:false).
        await projectorA.Received(4).RecomputeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Granularity>(), false, TargetDate, Arg.Any<CancellationToken>());
        await projectorB.Received(4).RecomputeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Granularity>(), false, TargetDate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_marks_the_run_failed_when_a_projector_throws()
    {
        _runRepository.FindByRunDateAsync(Arg.Any<RunDate>(), Arg.Any<CancellationToken>()).Returns((ReconciliationRun?)null);

        var failingProjector = Substitute.For<IReadModelProjector>();
        failingProjector.RecomputeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Granularity>(), Arg.Any<bool>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var handler = CreateHandler([failingProjector]);
        var result = await handler.Handle(new RunNightlyReconciliationCommand(TargetDate), CancellationToken.None);

        result.Outcome.Should().Be(ReconciliationOutcome.Failed);
    }
}
