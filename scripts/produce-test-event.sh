#!/usr/bin/env bash
# Simulates one of the platform's 35 consumed events (event-contract.md) being dual-published to
# Kafka — necessary because no upstream service actually implements Kafka dual-publish yet (only
# kart-recommendation-service's own clickstream has a real Kafka producer today). Every payload
# embeds `eventType`/`eventId`/`publisherService`/`occurredAt` as top-level JSON fields (not just
# headers) so this works with the plain `kafka-console-producer.sh` CLI, which has no easy way to
# set custom message headers — the same tolerant-reader fallback
# AnalyticsKafkaConsumerHostedService's own header-or-body-field reading already supports.
#
# Usage:
#   scripts/produce-test-event.sh <EventType> [KAFKA_CONTAINER]
#   scripts/produce-test-event.sh --all [KAFKA_CONTAINER]      # produce one of every event type once
#   scripts/produce-test-event.sh --malformed <topic> [KAFKA_CONTAINER]  # produce a deliberately invalid payload
#
# Examples:
#   scripts/produce-test-event.sh OrderCreated
#   scripts/produce-test-event.sh --all
set -euo pipefail

# Executed via `docker exec` INSIDE the broker's own container, so this must use the internal
# PLAINTEXT listener's own bind address ("kafka:29092", the docker-compose service-name/port this
# container advertises for that listener and can resolve for itself via Docker's embedded DNS) —
# NOT the EXTERNAL listener's host-facing port (9092/9292), whose advertised address
# ("localhost:<host-port>") only resolves correctly from the Docker host, not from inside any
# container. Using the wrong one causes the client to bootstrap successfully, receive metadata
# telling it to reconnect to the advertised address, then hang retrying that unreachable address
# forever.
KAFKA_CONTAINER="${2:-kart-analytics-kafka}"
BROKER="kafka:29092"

now_iso() { date -u +"%Y-%m-%dT%H:%M:%S.000Z"; }
new_uuid() { python3 -c "import uuid; print(uuid.uuid4())" 2>/dev/null || cat /proc/sys/kernel/random/uuid; }

produce() {
  local topic="$1"
  local key="$2"
  local payload="$3"
  echo "-> topic=$topic key=$key"
  echo "${key}:${payload}" | docker exec -i "$KAFKA_CONTAINER" /opt/kafka/bin/kafka-console-producer.sh \
    --bootstrap-server "$BROKER" --topic "$topic" \
    --property "parse.key=true" --property "key.separator=:"
}

produce_malformed() {
  local topic="$1"
  echo "-> malformed payload to topic=$topic"
  echo "malformed:{not-valid-json" | docker exec -i "$KAFKA_CONTAINER" /opt/kafka/bin/kafka-console-producer.sh \
    --bootstrap-server "$BROKER" --topic "$topic" \
    --property "parse.key=true" --property "key.separator=:"
}

# event_type -> "topic|publisherService|partitionKeyField|payload-template" (payload uses $EVENT_ID/$OCCURRED_AT)
emit() {
  local event_type="$1" topic="$2" publisher="$3" partition_key="$4" extra_json="$5"
  local event_id occurred_at payload
  event_id="$(new_uuid)"
  occurred_at="$(now_iso)"
  payload=$(cat <<EOF
{"eventType":"${event_type}","eventId":"${event_id}","publisherService":"${publisher}","occurredAt":"${occurred_at}",${extra_json}}
EOF
)
  produce "$topic" "$partition_key" "$payload"
}

produce_one() {
  case "$1" in
    OrderCreated) emit OrderCreated kart.order.events kart-order-service order-demo-1 '"orderId":"order-demo-1","userId":"user-demo-1","items":[{"sku":"sku-1","qty":1}],"total":42.50' ;;
    OrderConfirmed) emit OrderConfirmed kart.order.events kart-order-service order-demo-1 '"orderId":"order-demo-1","address":"123 Demo St"' ;;
    OrderCancelled) emit OrderCancelled kart.order.events kart-order-service order-demo-1 '"orderId":"order-demo-1","reason":"customer_requested"' ;;
    OrderCompensationTriggered) emit OrderCompensationTriggered kart.order.events kart-order-service order-demo-1 '"orderId":"order-demo-1","reason":"payment_failed"' ;;
    OrderDelivered) emit OrderDelivered kart.order.events kart-order-service order-demo-1 '"orderId":"order-demo-1","deliveredAt":"'"$(now_iso)"'"' ;;
    InventoryReserved) emit InventoryReserved kart.inventory.events kart-inventory-service order-demo-1 '"orderId":"order-demo-1","sku":"sku-1","qty":1' ;;
    InventoryReservationFailed) emit InventoryReservationFailed kart.inventory.events kart-inventory-service order-demo-1 '"orderId":"order-demo-1","sku":"sku-1"' ;;
    InventoryReleased) emit InventoryReleased kart.inventory.events kart-inventory-service order-demo-1 '"orderId":"order-demo-1","sku":"sku-1","qty":1' ;;
    InventoryReplenished) emit InventoryReplenished kart.inventory.events kart-inventory-service sku-1 '"sku":"sku-1","qtyAdded":100,"warehouseId":"wh-1"' ;;
    PaymentCompleted) emit PaymentCompleted kart.payment.events kart-payment-service order-demo-1 '"orderId":"order-demo-1","txnId":"txn-demo-1"' ;;
    PaymentFailed) emit PaymentFailed kart.payment.events kart-payment-service order-demo-1 '"orderId":"order-demo-1","reason":"card_declined"' ;;
    RefundIssued) emit RefundIssued kart.payment.events kart-payment-service order-demo-1 '"orderId":"order-demo-1","refundId":"refund-demo-1","amount":42.50' ;;
    ChargebackReceived) emit ChargebackReceived kart.payment.events kart-payment-service order-demo-1 '"orderId":"order-demo-1","paymentIntentId":"pi-demo-1","chargebackId":"cb-demo-1","amount":42.50,"reason":"fraud"' ;;
    ShipmentDispatched) emit ShipmentDispatched kart.shipping.events kart-shipping-service order-demo-1 '"orderId":"order-demo-1","carrier":"demo-carrier","trackingId":"track-demo-1"' ;;
    ShipmentCreationFailed) emit ShipmentCreationFailed kart.shipping.events kart-shipping-service order-demo-1 '"orderId":"order-demo-1","reason":"no_carrier_available"' ;;
    DeliveryStatusUpdated) emit DeliveryStatusUpdated kart.delivery-tracking.events kart-delivery-tracking-service track-demo-1 '"trackingId":"track-demo-1","status":"delivered"' ;;
    ProductCreated) emit ProductCreated kart.product.events kart-product-service sku-1 '"sku":"sku-1","name":"Demo Widget","description":"A demo widget","categoryId":"cat-1","brand":"DemoBrand","price":19.99,"status":"active","attributes":{}' ;;
    ProductPriceChanged) emit ProductPriceChanged kart.product.events kart-product-service sku-1 '"sku":"sku-1","oldPrice":19.99,"newPrice":17.99' ;;
    ProductUpdated) emit ProductUpdated kart.product.events kart-product-service sku-1 '"sku":"sku-1","changedFields":["description"],"name":"Demo Widget","description":"Updated description","categoryId":"cat-1","brand":"DemoBrand","status":"active","attributes":{}' ;;
    ReviewSubmitted) emit ReviewSubmitted kart.review.events kart-review-service order-demo-1 '"orderId":"order-demo-1","sku":"sku-1","rating":5,"reviewId":"review-demo-1","userId":"user-demo-1"' ;;
    ReviewUpdated) emit ReviewUpdated kart.review.events kart-review-service order-demo-1 '"orderId":"order-demo-1","sku":"sku-1","oldRating":5,"newRating":4' ;;
    CategoryUpdated) emit CategoryUpdated kart.category.events kart-category-service cat-1 '"categoryId":"cat-1","name":"Demo Category","parentId":null,"path":"/demo-category","operation":"update"' ;;
    CouponRedeemed) emit CouponRedeemed kart.offer.events kart-offer-service order-demo-1 '"code":"DEMO10","orderId":"order-demo-1"' ;;
    PriceQuoteIssued) emit PriceQuoteIssued kart.offer.events kart-offer-service quote-demo-1 '"quoteId":"quote-demo-1","total":39.99,"expiresAt":"'"$(now_iso)"'"' ;;
    PromotionActivated) emit PromotionActivated kart.offer.events kart-offer-service campaign-demo-1 '"campaignId":"campaign-demo-1","window":"2026-08-01/2026-08-31"' ;;
    PromotionDeactivated) emit PromotionDeactivated kart.offer.events kart-offer-service campaign-demo-1 '"campaignId":"campaign-demo-1"' ;;
    UserProfileUpdated) emit UserProfileUpdated kart.user.events kart-user-service user-demo-1 '"userId":"user-demo-1","changedFields":["displayName"]' ;;
    UserDataErased) emit UserDataErased kart.user.events kart-user-service user-demo-1 '"userId":"user-demo-1","erasedAt":"'"$(now_iso)"'"' ;;
    UserRegistered) emit UserRegistered kart.identity.events kart-identity-service user-demo-1 '"userId":"user-demo-1","email":"demo@example.com"' ;;
    SessionCreated) emit SessionCreated kart.identity.events kart-identity-service user-demo-1 '"userId":"user-demo-1","sessionId":"session-demo-1"' ;;
    UserAccountUpdated) emit UserAccountUpdated kart.identity.events kart-identity-service user-demo-1 '"userId":"user-demo-1","email":"demo@example.com","displayName":"Demo User","updatedAt":"'"$(now_iso)"'"' ;;
    NotificationSent) emit NotificationSent kart.notification.events kart-notification-service user-demo-1 '"userId":"user-demo-1","channel":"email","status":"sent"' ;;
    CartCheckedOut) emit CartCheckedOut kart.cart.events kart-cart-service cart-demo-1 '"cartId":"cart-demo-1","userId":"user-demo-1","items":[{"sku":"sku-1","qty":1}]' ;;
    WishlistPriceAlertTriggered) emit WishlistPriceAlertTriggered kart.wishlist.events kart-wishlist-service user-demo-1 '"userId":"user-demo-1","sku":"sku-1","oldPrice":19.99,"newPrice":17.99' ;;
    AdminActionPerformed) emit AdminActionPerformed kart.admin.events kart-admin-service admin-demo-1 '"adminId":"admin-demo-1","action":"catalog.update","entityId":"sku-1"' ;;
    *)
      echo "Unknown event type: $1" >&2
      echo "See event-contract.md / this script's case statement for the full list of 35 supported event types." >&2
      exit 1
      ;;
  esac
}

ALL_EVENT_TYPES=(
  OrderCreated OrderConfirmed OrderCancelled OrderCompensationTriggered OrderDelivered
  InventoryReserved InventoryReservationFailed InventoryReleased InventoryReplenished
  PaymentCompleted PaymentFailed RefundIssued ChargebackReceived
  ShipmentDispatched ShipmentCreationFailed DeliveryStatusUpdated
  ProductCreated ProductPriceChanged ProductUpdated
  ReviewSubmitted ReviewUpdated CategoryUpdated
  CouponRedeemed PriceQuoteIssued PromotionActivated PromotionDeactivated
  UserProfileUpdated UserDataErased UserRegistered SessionCreated UserAccountUpdated
  NotificationSent CartCheckedOut WishlistPriceAlertTriggered AdminActionPerformed
)

case "${1:-}" in
  --all)
    for eventType in "${ALL_EVENT_TYPES[@]}"; do
      produce_one "$eventType"
    done
    echo "Produced one of every ${#ALL_EVENT_TYPES[@]} supported event types."
    ;;
  --malformed)
    if [ -z "${2:-}" ]; then
      echo "Usage: $0 --malformed <topic> [KAFKA_CONTAINER]" >&2
      exit 1
    fi
    KAFKA_CONTAINER="${3:-kart-analytics-kafka}"
    produce_malformed "$2"
    ;;
  "")
    echo "Usage: $0 <EventType> | --all | --malformed <topic>" >&2
    exit 1
    ;;
  *)
    produce_one "$1"
    ;;
esac
