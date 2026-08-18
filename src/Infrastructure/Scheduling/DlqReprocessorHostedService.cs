using Kart.Analytics.Application.Features.ReprocessDlqEvents;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kart.Analytics.Infrastructure.Scheduling;

/// <summary>ANL-3: periodically drains `analytics_dlq_events` via <see cref="ReprocessDlqEventsCommand"/>.</summary>
public sealed class DlqReprocessorHostedService(IServiceScopeFactory scopeFactory, ILogger<DlqReprocessorHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(new ReprocessDlqEventsCommand(BatchSize), stoppingToken);

                if (result.Reprocessed > 0 || result.StillFailing > 0)
                {
                    logger.LogInformation("DLQ reprocessor: {Reprocessed} reprocessed, {StillFailing} still failing", result.Reprocessed, result.StillFailing);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DLQ reprocessor run failed");
            }
        }
    }
}
