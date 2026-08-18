namespace Kart.Analytics.Domain.ValueObjects;

/// <summary>
/// The wire-format version pointer for an ingested event's schema (requirement-spec.md §6 D2;
/// ddd-model.md). <paramref name="SchemaId"/> is what a real Confluent-compatible schema registry
/// would assign per publish; since this build uses the "JSON + tolerant reader" strategy (no real
/// registry stood up anywhere on the platform yet — confirmed decision, see plan) rather than
/// standing up unused Avro/registry infrastructure, <see cref="Kart.Analytics.Application.Common.SchemaVersioning.SchemaVersionResolver"/>
/// derives a stable pointer from the event's own declared shape instead of a registry round-trip —
/// the column still carries meaningful version metadata (satisfying the intent of D2), just sourced
/// differently than the literal design doc.
/// </summary>
/// <param name="SchemaId">The wire-format version pointer for this event instance.</param>
/// <param name="VersionLabel">Human-readable `MAJOR.MINOR` label — lets ingestion/ops distinguish an additive MINOR bump from a breaking MAJOR one in flight.</param>
public sealed record SchemaVersionPointer(string SchemaId, string VersionLabel)
{
    public static SchemaVersionPointer Create(string schemaId, string versionLabel)
    {
        if (string.IsNullOrWhiteSpace(schemaId))
        {
            throw new ArgumentException("Schema id must not be empty.", nameof(schemaId));
        }

        if (string.IsNullOrWhiteSpace(versionLabel))
        {
            throw new ArgumentException("Version label must not be empty.", nameof(versionLabel));
        }

        return new SchemaVersionPointer(schemaId, versionLabel);
    }
}
