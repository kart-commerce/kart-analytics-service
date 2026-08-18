#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
docker compose up -d --build
echo "kart-analytics-service stack starting. Postgres:5452 Mongo(router):27217 Kafka:9292 Service:8097"
echo "Once Mongo is up, run scripts/init-mongo-cluster.sh once to enable sharding."
echo "Tail logs: docker compose logs -f analytics-service"
