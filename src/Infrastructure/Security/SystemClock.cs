using Kart.Analytics.Application.Common.Interfaces;

namespace Kart.Analytics.Infrastructure.Security;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
