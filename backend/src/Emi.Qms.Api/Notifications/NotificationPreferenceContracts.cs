namespace Emi.Qms.Api.Notifications;

public sealed record NotificationPreferenceItemDefinition(
    string DeliveryType,
    string Channel,
    string EventLabel,
    string ChannelLabel,
    string Description,
    bool CanChange,
    string? LockReason);

public sealed record NotificationPreferenceItemResponse(
    string DeliveryType,
    string Channel,
    string EventLabel,
    string ChannelLabel,
    string Description,
    bool Enabled,
    bool CanChange,
    bool IsOverridden,
    string? LockReason);

public sealed record NotificationPreferenceResponse(
    Guid UserId,
    string UserDisplayName,
    string TaxonomyVersion,
    long Version,
    bool IsDefault,
    bool Changed,
    IReadOnlyList<NotificationPreferenceItemResponse> Items);

public sealed record NotificationPreferenceUpdateItem(
    string DeliveryType,
    string Channel,
    bool Enabled);

public sealed record UpdateNotificationPreferencesRequest(
    long ExpectedVersion,
    IReadOnlyList<NotificationPreferenceUpdateItem> Items);

public sealed record ResetNotificationPreferencesRequest(long ExpectedVersion);

public enum NotificationPreferenceResultStatus
{
    Success,
    NotFound,
    Inactive,
    Conflict,
    Invalid
}

public sealed record NotificationPreferenceResult(
    NotificationPreferenceResultStatus Status,
    NotificationPreferenceResponse? Response,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static NotificationPreferenceResult Success(NotificationPreferenceResponse response) =>
        new(NotificationPreferenceResultStatus.Success, response, null, null);

    public static NotificationPreferenceResult Failure(
        NotificationPreferenceResultStatus status,
        string errorCode,
        string errorMessage,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(status, null, errorCode, errorMessage, errors);
}
