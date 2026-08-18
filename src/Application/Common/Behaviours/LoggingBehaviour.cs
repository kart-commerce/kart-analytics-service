using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Analytics.Application.Common.Behaviours;

/// <summary>
/// Every command/query gets a structured Information log on completion, tagged with its own name
/// and duration (mirrors kart-identity-service's <c>LoggingBehaviour</c> exactly). Exceptions are
/// left unlogged here and rethrown as-is: they're logged once, at the true boundary
/// (<c>Kart.Shared.ErrorHandling</c>'s exception handler), not duplicated at every pipeline layer
/// they pass through.
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        logger.LogInformation(
            "{RequestName} completed in {ElapsedMilliseconds}ms",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
