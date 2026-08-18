namespace Kart.Analytics.Application.Common;

/// <summary>
/// Which of the 35 consumed event types (event-contract.md) carry end-user PII in their
/// documented payload, driving `analytics_raw_events.contains_pii` (database-design.md). No
/// design doc enumerates an exhaustive list (database-design.md's own wording is "e.g.
/// UserRegistered, SessionCreated, ReviewSubmitted, UserProfileUpdated, etc.") — this is this
/// service's own judgment call, derived directly from each event's documented payload key fields
/// in event-contract.md: an event is classified true only when its payload names a `userId` field
/// — the same field the redaction sweep (ANL-4) matches on ("the payload's userId matches",
/// database-design.md). `OrderConfirmed`'s `address` field is personal data too, but with no
/// `userId` in that event's documented payload there is nothing for a userId-keyed sweep to match
/// against, so it is deliberately left `false` here rather than flagged PII with no redaction path
/// — a future pass adding an address-specific redaction key could reclassify it then. Operational/
/// staff identifiers (`adminId` on `AdminActionPerformed` — BRD §24.1.5 explicitly classifies this
/// as "not customer PII") and events with no person-identifying field are classified false.
/// </summary>
public static class PiiEventClassification
{
    private static readonly HashSet<string> PiiBearingEventTypes = new(StringComparer.Ordinal)
    {
        "OrderCreated", // userId
        "ReviewSubmitted", // userId
        "UserProfileUpdated", // userId
        "UserDataErased", // userId
        "UserRegistered", // userId, email
        "SessionCreated", // userId
        "UserAccountUpdated", // userId, email, displayName
        "NotificationSent", // userId
        "CartCheckedOut", // userId
        "WishlistPriceAlertTriggered", // userId
    };

    public static bool ContainsPii(string eventType) => PiiBearingEventTypes.Contains(eventType);
}
