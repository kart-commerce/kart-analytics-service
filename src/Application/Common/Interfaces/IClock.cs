namespace Kart.Analytics.Application.Common.Interfaces;

/// <summary>Testable wall-clock seam — never call <c>DateTimeOffset.UtcNow</c> directly from a
/// handler/domain call site (kart-recommendation-service's own <c>IClock</c> convention).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
