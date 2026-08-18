using Kart.Analytics.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Analytics.Infrastructure.Persistence.Converters;

/// <summary>
/// database-design.md's `status` CHECK constraint uses lowercase snake_case vocabulary
/// ('running'/'completed'/'failed', not 'Running'/'Completed'/'Failed') — this converter keeps
/// the stored values byte-for-byte identical to the approved schema rather than relying on EF's
/// default enum-name string conversion. Static methods (not inline switch/throw expressions)
/// because <see cref="ValueConverter{TModel,TProvider}"/> compiles its lambdas as expression
/// trees, which can't contain either construct (mirrors kart-identity-service's own converter).
/// </summary>
internal static class EnumDbValueConverters
{
    public static readonly ValueConverter<RunStatus, string> RunStatus = new(
        v => RunStatusToDbValue(v),
        v => RunStatusFromDbValue(v));

    private static string RunStatusToDbValue(RunStatus v) => v switch
    {
        Domain.Enums.RunStatus.Running => "running",
        Domain.Enums.RunStatus.Completed => "completed",
        Domain.Enums.RunStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null),
    };

    private static RunStatus RunStatusFromDbValue(string v) => v switch
    {
        "running" => Domain.Enums.RunStatus.Running,
        "completed" => Domain.Enums.RunStatus.Completed,
        "failed" => Domain.Enums.RunStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null),
    };
}
