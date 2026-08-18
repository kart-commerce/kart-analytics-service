using FluentAssertions;
using Kart.Analytics.Domain.Entities;
using Kart.Analytics.Domain.Enums;
using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.UnitTests.Domain;

public class ReconciliationRunTests
{
    private static readonly RunDate SomeDate = RunDate.From(new DateOnly(2026, 8, 17));

    [Fact]
    public void StartNew_begins_in_Running_status()
    {
        var run = ReconciliationRun.StartNew(SomeDate, DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");
        run.Status.Should().Be(RunStatus.Running);
    }

    [Fact]
    public void Complete_transitions_from_Running_to_Completed()
    {
        var run = ReconciliationRun.StartNew(SomeDate, DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");
        run.Complete(DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");
        run.Status.Should().Be(RunStatus.Completed);
    }

    [Fact]
    public void Complete_throws_when_run_already_completed()
    {
        var run = ReconciliationRun.StartNew(SomeDate, DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");
        run.Complete(DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");

        var act = () => run.Complete(DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Retry_only_allowed_from_Failed_status()
    {
        var run = ReconciliationRun.StartNew(SomeDate, DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");

        var act = () => run.Retry(DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Failed_run_can_be_retried_and_then_completed()
    {
        var run = ReconciliationRun.StartNew(SomeDate, DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");
        run.Fail(DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");
        run.Status.Should().Be(RunStatus.Failed);

        run.Retry(DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");
        run.Status.Should().Be(RunStatus.Running);

        run.Complete(DateTimeOffset.UtcNow, "system:analytics-reconciliation-job");
        run.Status.Should().Be(RunStatus.Completed);
    }
}
