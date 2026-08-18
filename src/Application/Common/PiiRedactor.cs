using System.Text.Json.Nodes;

namespace Kart.Analytics.Application.Common;

/// <summary>
/// database-design.md "PII Redaction on UserDataErased": nulls known PII field names inside the
/// payload while preserving every other field verbatim (order totals, timestamps, event type) —
/// the redact-in-place approach that keeps replay/recompute aggregates stable. `userId` itself is
/// deliberately never nulled: it is an opaque identifier (not a directly-identifying value on its
/// own) and is still needed as the sweep's own correlation key and for future idempotent re-runs.
/// </summary>
public static class PiiRedactor
{
    private const string RedactedPlaceholder = "[redacted]";

    private static readonly string[] PiiFieldNames = ["email", "displayName", "name"];

    public static string Redact(string payloadJson)
    {
        var node = JsonNode.Parse(payloadJson)?.AsObject() ?? new JsonObject();

        foreach (var fieldName in PiiFieldNames)
        {
            if (node.ContainsKey(fieldName))
            {
                node[fieldName] = RedactedPlaceholder;
            }
        }

        return node.ToJsonString();
    }
}
