using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;

namespace Kart.Analytics.Application.Features.GetUserGrowthDashboard;

/// <summary>ANL-12: user_growth_dashboard. `profileChanges` sums both `UserProfileUpdated` and
/// `UserAccountUpdated` — both represent a change to a user's own profile data.</summary>
public sealed class UserGrowthDashboardProjector(
    IIngestedEventRepository ingestedEventRepository,
    IReadModelStore readModelStore,
    IClock clock) : IReadModelProjector
{
    public string DashboardKey => "user-growth";

    public async Task RecomputeAsync(DateTimeOffset windowFrom, DateTimeOffset windowTo, Granularity granularity, bool isProvisional, DateOnly? reconciledThrough, CancellationToken cancellationToken)
    {
        var signups = await ingestedEventRepository.GetByTypeInWindowAsync("UserRegistered", windowFrom, windowTo, cancellationToken);
        var sessions = await ingestedEventRepository.GetByTypeInWindowAsync("SessionCreated", windowFrom, windowTo, cancellationToken);
        var profileUpdates = await ingestedEventRepository.GetByTypeInWindowAsync("UserProfileUpdated", windowFrom, windowTo, cancellationToken);
        var accountUpdates = await ingestedEventRepository.GetByTypeInWindowAsync("UserAccountUpdated", windowFrom, windowTo, cancellationToken);

        var granularityLabel = BucketCalculator.ToGranularityLabel(granularity);
        var documentId = $"{granularityLabel}:{windowFrom:yyyy-MM-ddTHH}";

        var document = new UserGrowthDashboardReadModel
        {
            Id = documentId,
            Granularity = granularityLabel,
            BucketStart = windowFrom.UtcDateTime,
            GeneratedAt = clock.UtcNow.UtcDateTime,
            IsProvisional = isProvisional,
            ReconciledThrough = reconciledThrough?.ToDateTime(TimeOnly.MinValue),
            Signups = signups.Count,
            SessionsCreated = sessions.Count,
            ProfileChanges = profileUpdates.Count + accountUpdates.Count,
        };

        await readModelStore.UpsertAsync("user_growth_dashboard", documentId, document, cancellationToken);
    }
}
