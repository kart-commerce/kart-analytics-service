using Kart.Analytics.Application.Features.RunNightlyReconciliation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kart.Analytics.Infrastructure.Scheduling;

/// <summary>
/// The nightly batch reconciler half of the CQRS sync mechanism (architecture.md's proposed
/// 06:00 UTC completion target). Checked every 15 minutes rather than gated to a single
/// once-a-day wall-clock trigger: <see cref="RunNightlyReconciliationCommand"/> is itself
/// idempotent per `RunDate` (returns `AlreadyCompleted` immediately if today's target date is
/// already done), so a more frequent check only costs a cheap no-op lookup most of the time,
/// while also making the job runnable immediately at startup for local dev/verification rather
/// than only after waiting for a specific wall-clock hour to arrive.
/// </summary>
public sealed class NightlyReconciliationHostedService(IServiceScopeFactory scopeFactory, ILogger<NightlyReconciliationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(new RunNightlyReconciliationCommand(TargetDate: null), stoppingToken);

                if (result.Outcome == ReconciliationOutcome.Completed)
                {
                    logger.LogInformation("Nightly reconciliation completed for {TargetDate}", result.TargetDate);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Nightly reconciliation run failed");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }
}
