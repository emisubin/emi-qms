namespace Emi.Qms.Api.Notifications;

public static class NotificationDeliveryChannels
{
    public const string TeamsChannel = "TeamsChannel";
    public const string TeamsDirectMessage = "TeamsDirectMessage";
    public const string Mail = "Mail";
}

public static class NotificationDeliveryTypes
{
    public const string WorkItemCreated = "WorkItemCreated";
    public const string ReferenceDigest = "ReferenceDigest";
    public const string UrgentBlocking = "UrgentBlocking";
    public const string DailyDigest = "DailyDigest";
    public const string ProjectCompletion = "ProjectCompletion";
    public const string ManualTest = "ManualTest";
}

public static class NotificationDeliveryStatuses
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Suppressed = "Suppressed";
    public const string Disabled = "Disabled";
    public const string DryRunSent = "DryRunSent";
}

public sealed record NotificationDispatchSummary(
    int CreatedDeliveryCount,
    int CreatedDigestDeliveryCount,
    int ProcessedDeliveryCount);

public sealed record NotificationDeliveryRecord(
    Guid DeliveryId,
    Guid? NotificationId,
    Guid? NotificationRecipientId,
    Guid? RecipientUserId,
    Guid? ProjectId,
    Guid? WorkItemId,
    string Channel,
    string DeliveryType,
    string Status,
    int AttemptCount,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? SuppressedAtUtc,
    string? ErrorCode,
    string? ErrorMessage,
    string DedupeKey,
    string? GroupKey,
    string? ProviderMessageId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? RecipientDisplayName,
    string? RecipientEmail,
    string? NotificationTitle,
    string? NotificationMessage,
    string? LinkUrl,
    string? ProjectTitle,
    string? ProjectCode,
    string? NotificationType,
    string? Severity);

public sealed record NotificationDeliveryMessage(
    Guid DeliveryId,
    string Channel,
    string DeliveryType,
    string Subject,
    string Body,
    string? LinkUrl,
    string? RecipientDisplayName,
    string? RecipientEmail,
    bool SaveToSentItems = false,
    string? CorrelationId = null,
    string? SenderUserId = null,
    string? SenderAddress = null);

public sealed record NotificationChannelResult(
    string Status,
    string? ProviderMessageId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static NotificationChannelResult Sent(string? providerMessageId = null)
    {
        return new NotificationChannelResult(NotificationDeliveryStatuses.Sent, providerMessageId);
    }

    public static NotificationChannelResult DryRunSent()
    {
        return new NotificationChannelResult(NotificationDeliveryStatuses.DryRunSent, "dry-run");
    }

    public static NotificationChannelResult Disabled(string code, string message)
    {
        return new NotificationChannelResult(NotificationDeliveryStatuses.Disabled, ErrorCode: code, ErrorMessage: message);
    }

    public static NotificationChannelResult Suppressed(string code, string message)
    {
        return new NotificationChannelResult(NotificationDeliveryStatuses.Suppressed, ErrorCode: code, ErrorMessage: message);
    }

    public static NotificationChannelResult Failed(string code, string message)
    {
        return new NotificationChannelResult(NotificationDeliveryStatuses.Failed, ErrorCode: code, ErrorMessage: message);
    }
}

public sealed record NotificationDeliveryListResponse(IReadOnlyList<NotificationDeliveryResponse> Items);

public sealed record NotificationDeliveryResponse(
    Guid DeliveryId,
    string Channel,
    string DeliveryType,
    string Status,
    int AttemptCount,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? SuppressedAtUtc,
    string? ErrorCode,
    string? ErrorMessage,
    string? RecipientDisplayName,
    string? RecipientEmail,
    string? ProjectTitle,
    string? ProjectCode,
    string? NotificationTitle,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record NotificationTestMailRequest(
    string? RecipientEmail,
    string? Subject,
    string? Message,
    bool? SaveToSentItems,
    string? SubjectSuffix);

public sealed record NotificationTestMailResponse(
    Guid DeliveryId,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string Provider,
    string CorrelationId,
    string SenderSource,
    string SenderMasked,
    string RecipientSource,
    string RecipientMasked,
    int RecipientCount,
    bool SenderEqualsRecipient,
    bool SaveToSentItems);
