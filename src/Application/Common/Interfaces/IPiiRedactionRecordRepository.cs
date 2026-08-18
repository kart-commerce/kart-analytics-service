using Kart.Analytics.Domain.Entities;

namespace Kart.Analytics.Application.Common.Interfaces;

/// <summary>The sole repository for the immutable <see cref="PiiRedactionRecord"/> aggregate root.</summary>
public interface IPiiRedactionRecordRepository
{
    Task AddAsync(PiiRedactionRecord record, CancellationToken cancellationToken);
}
