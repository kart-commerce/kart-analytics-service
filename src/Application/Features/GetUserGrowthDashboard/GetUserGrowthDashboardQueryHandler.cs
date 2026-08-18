using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Application.Common.Models;
using MediatR;

namespace Kart.Analytics.Application.Features.GetUserGrowthDashboard;

public sealed class GetUserGrowthDashboardQueryHandler(IReadModelStore readModelStore, IClock clock)
    : IRequestHandler<GetUserGrowthDashboardQuery, UserGrowthDashboardResult>
{
    public async Task<UserGrowthDashboardResult> Handle(GetUserGrowthDashboardQuery request, CancellationToken cancellationToken)
    {
        var granularityLabel = BucketCalculator.ToGranularityLabel(request.Granularity);
        var fromUtc = request.From.UtcDateTime;
        var toUtc = request.To.UtcDateTime;

        var documents = await readModelStore.QueryAsync<UserGrowthDashboardReadModel>(
            "user_growth_dashboard",
            q => q.Where(d => d.Granularity == granularityLabel && d.BucketStart >= fromUtc && d.BucketStart < toUtc),
            cancellationToken);

        var envelope = DashboardEnvelopeBuilder.Build(documents, clock.UtcNow);
        return new UserGrowthDashboardResult(envelope, documents.Sum(d => d.Signups), documents.Sum(d => d.SessionsCreated), documents.Sum(d => d.ProfileChanges));
    }
}
