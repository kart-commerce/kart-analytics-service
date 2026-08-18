namespace Kart.Analytics.Domain.ValueObjects;

/// <summary>
/// Shared shape every Guid-backed strongly-typed entity ID in this domain implements (mirrors
/// kart-identity-service's <c>ITypedEntityId&lt;TSelf&gt;</c> pattern exactly). Exists so
/// Infrastructure's generic value converter can map any of them to/from a `uuid` column without a
/// bespoke <c>ValueConverter</c> per ID type — the identity concept itself (a validated wrapper
/// around a single Guid) lives here in the domain; Infrastructure only needs a uniform way to
/// unwrap/rewrap it. This is this service's primitive-obsession fix for entity identity: a
/// <see cref="Kart.Analytics.Domain.ValueObjects.DlqId"/> can never be passed where an
/// <see cref="Kart.Analytics.Domain.ValueObjects.EventId"/> is expected without the compiler
/// catching it.
/// </summary>
public interface ITypedEntityId<TSelf> where TSelf : struct, ITypedEntityId<TSelf>
{
    Guid Value { get; }

    static abstract TSelf From(Guid value);
}
