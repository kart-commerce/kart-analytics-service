using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kart.Analytics.Domain.ValueObjects;

namespace Kart.Analytics.Application.Common.SchemaVersioning;

/// <summary>
/// requirement-spec.md §6 D2 / design-decisions.md's "Serialization Format &amp; Schema
/// Governance" decision specifies a real Confluent-compatible schema registry (Avro,
/// registry-assigned schema id, `BACKWARD` compatibility mode). This build uses the confirmed
/// "JSON + tolerant reader" strategy instead (no service anywhere on this platform stands up a
/// real schema registry yet — see the build plan's decision record) — deliberately not
/// over-engineering unused registry/Avro infrastructure while still populating
/// <see cref="SchemaVersionPointer"/> with a meaningful value: a stable content-shape fingerprint
/// derived from the payload's own top-level property names, sorted, so two payloads with the same
/// shape resolve to the same <see cref="SchemaVersionPointer.SchemaId"/> and a genuinely different
/// shape (a publisher adding/removing/renaming a top-level field) resolves to a different one —
/// the same "detect a shape change" signal a real registry's compatibility check would flag,
/// without requiring one to be running.
/// </summary>
public static class SchemaVersionResolver
{
    private const string BaselineVersionLabel = "1.0";

    public static SchemaVersionPointer Resolve(string payloadJson)
    {
        var propertyNames = ExtractSortedTopLevelPropertyNames(payloadJson);
        var schemaId = ComputeShapeFingerprint(propertyNames);
        return SchemaVersionPointer.Create(schemaId, BaselineVersionLabel);
    }

    private static IReadOnlyList<string> ExtractSortedTopLevelPropertyNames(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static string ComputeShapeFingerprint(IReadOnlyList<string> sortedPropertyNames)
    {
        var shapeSignature = string.Join(",", sortedPropertyNames);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(shapeSignature));
        return Convert.ToHexString(hash)[..16];
    }
}
