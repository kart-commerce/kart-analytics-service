# Contracts

`api-contract.yaml`, `event-contract.md`, and `message-bus-manifest.json` are synced copies of
the approved contracts owned by `kart-platform/docs/services/kart-analytics-service/` (the
source of truth). They are vendored here so `tests/ContractTests` can validate this service's
actual HTTP responses against the API contract in this repo's own CI, without a cross-repo
checkout, and so the Kafka-only manifest travels with the built artifact the same way
`kart-identity-service`'s RabbitMQ manifest does. Update them only by re-copying the upstream
files after a new contract revision is approved there — never edit them directly in this repo.

`message-bus-manifest.json`'s `"transport": "kafka-only"` confirms this service owns no
RabbitMQ exchange/DLX/queue at all — every consumed event arrives via Kafka (see
`event-contract.md`'s full fan-in list and this repo's own
`src/Infrastructure/Messaging/Kafka/KafkaOptions.cs` for the concrete topic names this build
subscribes to).
