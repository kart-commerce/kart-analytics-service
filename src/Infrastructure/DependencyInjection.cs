using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Infrastructure.Auditing;
using Kart.Analytics.Infrastructure.Messaging.Kafka;
using Kart.Analytics.Infrastructure.Persistence;
using Kart.Analytics.Infrastructure.Persistence.Mongo;
using Kart.Analytics.Infrastructure.Persistence.Repositories;
using Kart.Analytics.Infrastructure.Scheduling;
using Kart.Analytics.Infrastructure.Security;
using Kart.Shared.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AnalyticsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("AnalyticsDb")));

        services.AddScoped<IIngestedEventRepository, IngestedEventRepository>();
        services.AddScoped<IDeadLetteredEventRepository, DeadLetteredEventRepository>();
        services.AddScoped<IReconciliationRunRepository, ReconciliationRunRepository>();
        services.AddScoped<IPiiRedactionRecordRepository, PiiRedactionRecordRepository>();
        services.AddSingleton<IClock, SystemClock>();

        // This service's own explicit BRD §24.3 audit-trail requirement: a REAL sink
        // (`analytics_audit_log`), not the Kart.Shared.Auditing NullAuditLogWriter default.
        services.AddKartAuditing<PostgresAuditLogWriter>();

        AddMongo(services, configuration);
        AddKafka(services, configuration);
        AddScheduledJobs(services);
        AddSecurity(services, configuration);

        return services;
    }

    private static void AddSecurity(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));
        services.AddMemoryCache();
        services.AddHttpClient(nameof(JwksSigningKeyResolver));
        services.AddSingleton<JwksSigningKeyResolver>();
    }

    private static void AddMongo(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<MongoContext>();
        services.AddHostedService<MongoIndexInitializerHostedService>();

        services.AddScoped<IReadModelStore, MongoReadModelStore>();
    }

    private static void AddKafka(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .ValidateOnStart();

        services.AddHostedService<AnalyticsKafkaConsumerHostedService>();
    }

    private static void AddScheduledJobs(IServiceCollection services)
    {
        services.AddHostedService<DlqReprocessorHostedService>();
        services.AddHostedService<NightlyReconciliationHostedService>();
        services.AddHostedService<IncrementalProjectionHostedService>();
    }
}
