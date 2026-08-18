using System.Reflection;
using FluentValidation;
using Kart.Analytics.Application.Common.Behaviours;
using Kart.Analytics.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Analytics.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Registration order is pipeline order (outermost first) — Logging wraps Validation
            // so a rejected/invalid request is still observed, not just a handler's own success path.
            cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });
        services.AddValidatorsFromAssembly(assembly);

        // One IReadModelProjector per D4a dashboard/funnel (ANL-6..ANL-15), registered by
        // scanning rather than one line per dashboard — adding an 11th dashboard later means only
        // adding its own projector class, never touching this registration (OCP), the same
        // "iterate every registered projector" contract RunNightlyReconciliation/
        // RunIncrementalProjection both already depend on.
        foreach (var projectorType in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IReadModelProjector).IsAssignableFrom(t)))
        {
            services.AddScoped(typeof(IReadModelProjector), projectorType);
        }

        return services;
    }
}
