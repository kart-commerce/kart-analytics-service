# kart-analytics-service

Platform-wide event ingestion and reporting (BRD §2.1 item 19). A Generic Subdomain,
**consumer-only** service: it ingests the full fan-in of every published platform event (35 event
types across 15 publishers, ADR-0004) into a durable Postgres raw-event store, then serves ten
pre-aggregated MongoDB dashboard/funnel read models through an internal-only, read-only REST API.
It owns no RabbitMQ exchange, has no public Gateway route, and publishes nothing. See `contracts/`
for the full API/event contracts and `kart-platform/docs/services/kart-analytics-service/` for the
design record this implementation follows.

## Architecture

- **.NET 8**, Clean/Onion layering: `src/Domain` (4 aggregate roots: `IngestedEvent`,
  `DeadLetteredEvent`, `ReconciliationRun`, `PiiRedactionRecord` — zero dependencies) →
  `src/Application` (MediatR vertical slices, one per ANL-* ticket) → `src/Infrastructure`
  (EF Core/Postgres, MongoDB, Kafka, JWT/JWKS) → `src/Api` (ASP.NET Core minimal-API host).
- **CQRS**: write side is PostgreSQL (`analytics_raw_events`/`analytics_dlq_events`/
  `analytics_reconciliation_runs`/`analytics_pii_redactions`), source of truth. Read side is
  sharded MongoDB (ten dashboard/funnel collections, sharded on `{granularity, bucketStart}`),
  always rebuilt from Postgres by two projection consumers — never written to by the query API.
  Every dashboard response carries `generatedAt`/`isProvisional`/`reconciledThrough`, surfacing
  the eventual-consistency window rather than hiding it.
- **Idempotency / no double-counting**: `analytics_raw_events.event_id` is the sole PK, upserted
  atomically (`INSERT ... ON CONFLICT DO UPDATE`, Postgres' `xmax=0` trick reports fresh-insert vs.
  replay-overwrite in one round trip — no find-then-branch race window). Every dashboard/funnel
  bucket is always **fully recomputed** from raw storage, never an incrementally-mutated counter —
  the actual mechanism that makes replay safe. Kafka offsets are committed only after a
  successful raw-store write or a successful DLQ hand-off, never left uncommitted.
- **Reused platform infrastructure**: `Kart.Shared.Configuration` (GlobalConfig),
  `Kart.Shared.Observability` (Serilog + OpenTelemetry, standard sampling tier),
  `Kart.Shared.ErrorHandling` (global exception handler + ProblemDetails), `Kart.Shared.Auditing`
  (a REAL Postgres-backed sink, `PostgresAuditLogWriter`, for this service's own low-volume
  operational actions — DLQ reprocessing, PII redaction, reconciliation transitions — not the
  `NullAuditLogWriter` default).
- **Kafka-only ingestion** (`message-bus-manifest.json`: `"transport": "kafka-only"`) — no
  `Kart.Shared.Messaging` (that package is RabbitMQ-only); consumed directly via `Confluent.Kafka`,
  one consumer group subscribed across the union of every publisher's own topic
  (`kart.<service>.events`), dispatched by the `eventType` message header/body field to a single
  generic ingestion handler.

### Four confirmed build decisions (deliberate deviations from the literal design docs — see plan/PR description for the full rationale)

1. **.NET 8**, not the BRD's aspirational .NET 9 — matches every real service in the monorepo.
2. **JSON + tolerant reader**, not a real Confluent Schema Registry/Avro — nothing else on the
   platform stands up a schema registry yet; `SchemaVersionResolver` still populates a meaningful
   `schemaId`/`schemaVersionLabel` (a content-shape fingerprint) so the versioning columns aren't
   empty placeholders.
3. **Real sharded MongoDB topology** (config-server + 2 shard replsets + mongos router, mirroring
   `kart-product-service`), sharded on `{granularity, bucketStart}` — even though this service's
   own `database-design.md` says sharding isn't load-bearing yet at its current scale.
4. **`analytics_raw_events` ships as a normal (non-partitioned) table**, not the design doc's
   illustrative `PARTITION BY RANGE (ingested_at)` — native Postgres partitioning requires every
   unique/PK constraint to include the partition key, which would force a composite
   `(event_id, ingested_at)` key and reopen the exact double-counting race the idempotent-upsert
   design exists to prevent. Idempotency was prioritized over partitioning, which the design doc
   itself calls "a later cost-optimization detail."

## Local development

```bash
cp src/Api/appsettings.Local.json.example src/Api/appsettings.Local.json   # point GlobalConfig:Path at your machine's shared secrets file
scripts/dev-up.sh                 # docker compose up --build: Postgres, sharded Mongo, Kafka, this service
scripts/init-mongo-cluster.sh     # once, after Mongo is up: enables sharding on the 9 bucketed collections
scripts/migrate.sh                # apply EF Core migrations (or let Dockerfile.migrate's bundle do it in CI)
scripts/dev-down.sh
```

Manual end-to-end verification (produce a real event onto a real Kafka topic against whichever
stack you're running — necessary since no upstream publisher dual-publishes to Kafka yet):

```bash
scripts/produce-test-event.sh OrderCreated
scripts/produce-test-event.sh --all                  # one of every supported event type
scripts/produce-test-event.sh --malformed kart.order.events   # exercise the DLQ path
```

Then inspect `analytics_raw_events`/`analytics_dlq_events` directly, or query a dashboard:

```bash
curl -H "Authorization: Bearer <token with analytics.dashboards.read scope>" \
  "http://localhost:8097/internal/v1/dashboards/revenue?from=2026-01-01T00:00:00Z&to=2026-12-31T00:00:00Z&granularity=Day"
```

## Testing

```bash
dotnet test
```

- `tests/UnitTests` — domain invariants (idempotent upsert branching, reconciliation state
  machine, PII redaction guards), command/query handlers with mocked repositories, pure helpers
  (bucket math, percentiles, schema fingerprinting, PII redaction).
- `tests/IntegrationTests` — real Testcontainers-backed Postgres, MongoDB, and Kafka: produces
  real events onto real topics and asserts against the real migrated schema. Covers
  fresh-insert-vs-replay idempotency (no duplicate rows on redelivery), the malformed-message →
  DLQ → offset-still-committed → next-message-still-processed path, and the
  `UserDataErased` → redact-in-place → `PiiRedactionRecord` path end-to-end.
- `tests/ContractTests` — live HTTP response shape validated against `contracts/api-contract.yaml`
  (every endpoint's `DashboardEnvelope` + its own declared fields, scope-gated auth) against a real
  Postgres/Mongo pair with an empty dataset.

## Known follow-ups (not yet done)

- **API Gateway routing**: not applicable by design — this API has no public Gateway route
  (requirement-spec.md §1), reachable only from an internal network segment.
- **Real upstream Kafka dual-publish**: none of the 15 publishing services actually dual-publish
  to Kafka yet on this platform — `scripts/produce-test-event.sh` simulates it for local
  verification. Once a publisher ships its own Outbox → Kafka dual-publish, this service's
  `KafkaOptions.DefaultTopics` list should be reconciled against that publisher's real topic name.
- **Formal load testing** against the proposed (non-blocking, architecture.md) P95<60s/P99<5min
  ingestion-lag and P95<2s/P99<5s dashboard-query budgets hasn't been run.
- **Cold-storage tiering** for `analytics_raw_events` (D3's "tiered hot/cold storage as a later
  cost-optimization detail") is not implemented — the table currently retains everything
  indefinitely in a single, non-partitioned table (see build decision #4 above).
- **Real Confluent Schema Registry** integration (design-decisions.md D2's literal Avro/registry
  design) is deferred — see build decision #2 above.
