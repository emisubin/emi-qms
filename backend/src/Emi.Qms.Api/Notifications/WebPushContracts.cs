using Emi.Qms.Api.BusinessUnits;

namespace Emi.Qms.Api.Notifications;

public sealed record WebPushSubscriptionKeysRequest(string P256dh, string Auth);

public sealed record WebPushSubscriptionRequest(
    string Endpoint,
    WebPushSubscriptionKeysRequest Keys);

public sealed record WebPushSubscriptionEndpointRequest(string Endpoint);

public sealed record WebPushDeactivateCurrentRequest(string Endpoint, string? Reason);

public sealed record WebPushConfigurationResponse(
    bool Enabled,
    bool DryRun,
    bool Configured,
    string? PublicKey,
    int ActiveDeviceCount,
    DateTimeOffset? LastChangedAtUtc);

public sealed record WebPushCurrentSubscriptionResponse(bool Active);

public sealed record WebPushSubscriptionMutationResponse(
    bool Active,
    int ActiveDeviceCount,
    DateTimeOffset ChangedAtUtc);

public sealed record WebPushDeliveryTarget(
    Guid SubscriptionId,
    long Generation,
    string Endpoint,
    string P256dh,
    string Auth,
    bool SubscriptionIsActive,
    bool UserIsActive);

public interface IWebPushSubscriptionDeliveryStore
{
    Task<WebPushDeliveryTarget?> GetDeliveryTargetAsync(Guid deliveryId, CancellationToken cancellationToken, BusinessUnitDatabaseTarget? target = null);

    Task RecordProviderAcceptedAsync(Guid subscriptionId, long expectedGeneration, CancellationToken cancellationToken, BusinessUnitDatabaseTarget? target = null);

    Task RecordProviderFailureAsync(Guid subscriptionId, long expectedGeneration, string failureCode, CancellationToken cancellationToken, BusinessUnitDatabaseTarget? target = null);

    Task DeactivateForProviderAsync(Guid subscriptionId, long expectedGeneration, string reason, CancellationToken cancellationToken, BusinessUnitDatabaseTarget? target = null);
}
