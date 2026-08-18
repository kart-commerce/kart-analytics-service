#!/usr/bin/env bash
# Initializes the sharded MongoDB cluster brought up by docker-compose.yml: one config-server
# replset, two shard replsets, one mongos router — then shards each of the nine time-bucketed
# dashboard/funnel collections on {granularity, bucketStart} (database-design.md's own named
# revisit-trigger key). `admin_audit_log` is a log, not a bucket aggregate, and is left unsharded
# (single shard is enough for its own _id-keyed access pattern). Run this once, after
# `docker compose up -d` and before the API starts consuming Mongo. Safe to re-run
# (rs.initiate/sh.addShard/sh.shardCollection are idempotent — Mongo returns an "already
# initialized"/"already sharded" error that this script tolerates), mirrors
# kart-product-service's own cluster init script.
set -euo pipefail

wait_for_mongo() {
  local container=$1
  local port=$2
  echo "Waiting for $container:$port ..."
  for _ in $(seq 1 30); do
    if docker exec "$container" mongosh --quiet --port "$port" --eval "db.runCommand('ping')" >/dev/null 2>&1; then
      echo "$container:$port is up"
      return 0
    fi
    sleep 2
  done
  echo "Timed out waiting for $container:$port" >&2
  exit 1
}

wait_for_mongo kart-analytics-mongo-configsvr 27019
wait_for_mongo kart-analytics-mongo-shard1 27018
wait_for_mongo kart-analytics-mongo-shard2 27018

echo "Initiating config server replica set..."
docker exec kart-analytics-mongo-configsvr mongosh --quiet --port 27019 --eval '
  try {
    rs.initiate({ _id: "analyticsCfgRS", configsvr: true, members: [{ _id: 0, host: "kart-analytics-mongo-configsvr:27019" }] });
  } catch (e) {
    if (!String(e).includes("already initialized")) { throw e; }
  }
'

echo "Initiating shard 1 replica set..."
docker exec kart-analytics-mongo-shard1 mongosh --quiet --port 27018 --eval '
  try {
    rs.initiate({ _id: "analyticsShard1RS", members: [{ _id: 0, host: "kart-analytics-mongo-shard1:27018" }] });
  } catch (e) {
    if (!String(e).includes("already initialized")) { throw e; }
  }
'

echo "Initiating shard 2 replica set..."
docker exec kart-analytics-mongo-shard2 mongosh --quiet --port 27018 --eval '
  try {
    rs.initiate({ _id: "analyticsShard2RS", members: [{ _id: 0, host: "kart-analytics-mongo-shard2:27018" }] });
  } catch (e) {
    if (!String(e).includes("already initialized")) { throw e; }
  }
'

echo "Waiting for replica sets to elect a primary..."
sleep 10

echo "Waiting for mongos router..."
wait_for_mongo kart-analytics-mongo-router 27017

echo "Adding shards to the cluster..."
docker exec kart-analytics-mongo-router mongosh --quiet --port 27017 --eval '
  try { sh.addShard("analyticsShard1RS/kart-analytics-mongo-shard1:27018"); } catch (e) { if (!String(e).includes("duplicate")) { print(e); } }
  try { sh.addShard("analyticsShard2RS/kart-analytics-mongo-shard2:27018"); } catch (e) { if (!String(e).includes("duplicate")) { print(e); } }
'

echo "Enabling sharding on the kart_analytics database and sharding the nine bucketed dashboard/funnel collections on {granularity, bucketStart}..."
docker exec kart-analytics-mongo-router mongosh --quiet --port 27017 --eval '
  sh.enableSharding("kart_analytics");

  const bucketedCollections = [
    "order_conversion_funnel",
    "revenue_dashboard",
    "fulfillment_performance_dashboard",
    "inventory_movement_dashboard",
    "catalog_pricing_dashboard",
    "promotions_effectiveness_dashboard",
    "user_growth_dashboard",
    "reviews_ratings_dashboard",
    "notification_delivery_dashboard",
  ];

  const db = db.getSiblingDB("kart_analytics");
  for (const name of bucketedCollections) {
    db[name].createIndex({ granularity: 1, bucketStart: 1 });
    try {
      sh.shardCollection(`kart_analytics.${name}`, { granularity: 1, bucketStart: 1 });
    } catch (e) {
      if (!String(e).includes("already sharded")) { print(e); }
    }
  }
'

echo "Sharding status:"
docker exec kart-analytics-mongo-router mongosh --quiet --port 27017 --eval 'sh.status()'

echo "Analytics Mongo cluster initialized."
