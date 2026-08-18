using System.Text.Json;

namespace Kart.Analytics.Application.Common;

/// <summary>
/// Small tolerant-reader convenience wrapper over a raw event's JSON payload (edge-cases.md
/// "Schema Evolution") — every projector reads a handful of top-level fields from `payload`
/// (ddd-model.md's opaque-JSON Anti-Corruption Layer: still never decomposed into a typed domain
/// model, just read positionally for aggregation). A missing/mistyped field returns the given
/// default rather than throwing, so one publisher's schema drift degrades that one field, not the
/// whole recompute.
/// </summary>
public sealed class PayloadReader(string payloadJson)
{
    private readonly JsonElement _root = JsonDocument.Parse(payloadJson).RootElement;

    public string? GetString(string name) =>
        _root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public decimal GetDecimal(string name, decimal defaultValue = 0m) =>
        _root.TryGetProperty(name, out var value) && value.TryGetDecimal(out var d) ? d : defaultValue;

    public int GetRatingInt(string name, int defaultValue = 0) =>
        _root.TryGetProperty(name, out var value) && value.TryGetInt32(out var i) ? i : defaultValue;
}
