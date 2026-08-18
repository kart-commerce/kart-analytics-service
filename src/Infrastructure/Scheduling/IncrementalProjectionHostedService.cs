using Kart.Analytics.Application.Features.RunIncrementalProjection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kart.Analytics.Infrastructure.Scheduling;

/// <summary>The near-real-time half of the CQRS sync mechanism (database-design.md): runs
/// <see cref="RunIncrementalProjectionCommand"/> on a short interval so dashboards look live
/// between nightly reconciliation passes.</summary>
public sealed class IncrementalProjectionHostedService(IServiceScopeFactory scopeFactory, ILogger<IncrementalProjectionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new RunIncrementalProjectionCommand(), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Incremental projection run failed");
            }
        }
    }
}
