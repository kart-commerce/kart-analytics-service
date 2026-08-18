using MediatR;

namespace Kart.Analytics.Application.Features.IngestEvent;

/// <summary>
/// ANL-1: ingest a single platform event into the raw store. One generic command handles all ~35
/// consumed event types (event-contract.md) — keyed by <paramref name="EventType"/>/schema, never
/// per-event code (tickets.md's own instruction for this ticket).
/// </summary>
/// <param name="EventId">Publisher-assigned event id — the idempotency/dedup key.</param>
/// <param name="EventType">e.g. "OrderCreated" — no allowlist, full fan-in per ADR-0004.</param>
/// <param name="PublisherService">e.g. "kart-order-service".</param>
/// <param name="PartitionKey">The Kafka partition key the publisher used (aggregate/entity id).</param>
/// <param name="OccurredAt">The publisher's own event-time timestamp.</param>
/// <param name="PayloadJson">The event's raw JSON body, as received.</param>
public sealed record IngestEventCommand(
    Guid EventId,
    string EventType,
    string PublisherService,
    string PartitionKey,
    DateTimeOffset OccurredAt,
    string PayloadJson) : IRequest<IngestEventResult>;

public sealed record IngestEventResult(bool WasFreshInsert);
