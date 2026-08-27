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
public sealed class PayloadReader
{
    private readonly JsonElement _root;

    public PayloadReader(string payloadJson) : this(JsonDocument.Parse(payloadJson).RootElement)
    {
    }

    private PayloadReader(JsonElement root)
    {
        _root = root;
    }

    public string? GetString(string name) =>
        _root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public decimal GetDecimal(string name, decimal defaultValue = 0m) =>
        _root.TryGetProperty(name, out var value) && value.TryGetDecimal(out var d) ? d : defaultValue;

    public int GetRatingInt(string name, int defaultValue = 0) =>
        _root.TryGetProperty(name, out var value) && value.TryGetInt32(out var i) ? i : defaultValue;

    /// <summary>Reads a nested object field (e.g. `OrderCreated.items[].unitPrice`) as its own
    /// tolerant reader, or null if the field is absent/not an object — used by
    /// `ProductPerformanceDashboardProjector` to reach into a line item's `unitPrice`.</summary>
    public PayloadReader? GetObject(string name) =>
        _root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? new PayloadReader(value) : null;

    /// <summary>Reads an array field (e.g. `OrderCreated.items`) as a list of per-element
    /// tolerant readers, or empty if the field is absent/not an array — never throws on a missing
    /// or malformed array, same "degrade this field, not the whole recompute" tolerance as every
    /// other accessor here.</summary>
    public IReadOnlyList<PayloadReader> GetArray(string name) =>
        _root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(element => new PayloadReader(element)).ToList()
            : [];
}
