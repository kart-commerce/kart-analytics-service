using MediatR;

namespace Kart.Analytics.Application.Features.ReprocessDlqEvents;

/// <summary>ANL-3: drains `analytics_dlq_events`, replaying each parked event through the same
/// ingestion path (ANL-1) — the BRD's own 30-day-reprocessing-scenario tooling, applied to this
/// service's own post-ingestion write failures.</summary>
public sealed record ReprocessDlqEventsCommand(int BatchSize) : IRequest<ReprocessDlqEventsResult>;

public sealed record ReprocessDlqEventsResult(int Reprocessed, int StillFailing);
