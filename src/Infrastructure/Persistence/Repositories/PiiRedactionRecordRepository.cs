using Kart.Analytics.Application.Common.Interfaces;
using Kart.Analytics.Domain.Entities;

namespace Kart.Analytics.Infrastructure.Persistence.Repositories;

/// <summary>The sole repository for the immutable <see cref="PiiRedactionRecord"/> aggregate root.</summary>
public sealed class PiiRedactionRecordRepository(AnalyticsDbContext dbContext) : IPiiRedactionRecordRepository
{
    public async Task AddAsync(PiiRedactionRecord record, CancellationToken cancellationToken)
    {
        dbContext.PiiRedactionRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
