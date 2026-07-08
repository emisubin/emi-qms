namespace Emi.Qms.Api.Notifications;

public static class NotificationDeliveryChannels
{
    public const string TeamsChannel = "TeamsChannel";
    public const string TeamsDirectMessage = "TeamsDirectMessage";
    public const string TeamsActivity = "TeamsActivity";
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
    public const string DueSoonL0 = "DueSoonL0";
    public const string OverdueL1 = "OverdueL1";
    public const string OverdueL2 = "OverdueL2";
    public const string OverdueL3 = "OverdueL3";
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

public static class NotificationDeliveryAdminHandlingStatuses
{
    public const string Open = "Open";
    public const string Acknowledged = "Acknowledged";
    public const string Dismissed = "Dismissed";
}

public static class NotificationManualKinds
{
    public const string ProjectCreated = "ProjectCreated";
    public const string WorkItemAssigned = "WorkItemAssigned";
    public const string Urgent = "Urgent";
    public const string DailyDigest = "DailyDigest";
    public const string Custom = "Custom";
}

public static class NotificationManualSendModes
{
    public const string Personal = "Personal";
    public const string ChannelNotice = "ChannelNotice";
    public const string WorkAssignment = "WorkAssignment";
}

public static class NotificationVisibilityScopes
{
    public const string RecipientOnly = "RecipientOnly";
    public const string Authenticated = "Authenticated";
    public const string AdminOnly = "AdminOnly";
}

public static class NotificationSourceKinds
{
    public const string Automatic = "Automatic";
    public const string Manual = "Manual";
    public const string ChannelNotice = "ChannelNotice";
    public const string WorkAssignment = "WorkAssignment";
    public const string DailyDigest = "DailyDigest";
    public const string Escalation = "Escalation";
    public const string System = "System";
}

public static class NotificationDisplayRecipientKinds
{
    public const string User = "User";
    public const string Email = "Email";
    public const string TeamsChannel = "TeamsChannel";
    public const string Unknown = "Unknown";
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
    string? Severity,
    string? RecipientEntraObjectId,
    string? RecipientAuthProvider,
    bool? RecipientUserIsActive,
    string? WorkItemTitle,
    string? WorkflowStageName,
    string? AdminHandlingStatus,
    DateTimeOffset? AdminHandledAtUtc,
    Guid? AdminHandledByUserId,
    string? AdminHandledByDisplayName,
    string? AdminHandlingNote,
    string? DisplayTitle,
    string? DisplayMessage,
    string? DisplayProjectName,
    string? DisplayWorkItemTitle,
    string? DisplayRecipientName,
    string? DisplayRecipientEmail,
    string? DisplayRecipientKind,
    string? DisplayChannelTarget,
    string? ManualNotificationKind,
    string? CorrelationId,
    string? ManualPayloadJson,
    Guid? ManualRequestedByUserId,
    DateTimeOffset? ManualRequestedAtUtc);

public sealed record NotificationDeliveryDisplaySnapshot(
    string? DisplayTitle,
    string? DisplayMessage,
    string? DisplayProjectName,
    string? DisplayWorkItemTitle,
    string? DisplayRecipientName,
    string? DisplayRecipientEmail,
    string? DisplayRecipientKind,
    string? DisplayChannelTarget,
    string? ManualNotificationKind,
    string? CorrelationId);

public sealed record NotificationManualPayload(
    string NotificationKind,
    string NotificationKindLabel,
    string Title,
    string? ProjectName,
    string Message,
    DateTimeOffset RequestedAtUtc);

public sealed record ManualNotificationProjectSnapshot(
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode);

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
    string? SenderAddress = null,
    Guid? RecipientUserId = null,
    string? RecipientEntraObjectId = null,
    string? RecipientAuthProvider = null,
    bool? RecipientUserIsActive = null,
    string? TeamsActivityType = null,
    string? TeamsActivityTopicSource = null,
    string? TeamsActivityTopicValue = null);

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
    Guid? NotificationId,
    Guid? RecipientUserId,
    Guid? ProjectId,
    Guid? WorkItemId,
    string Channel,
    string ChannelLabel,
    string DeliveryType,
    string DeliveryTypeLabel,
    string Status,
    string StatusLabel,
    int AttemptCount,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? SuppressedAtUtc,
    string? ErrorCode,
    string? ErrorMessage,
    string ActionGuide,
    string? PendingReason,
    string? RecipientDisplayName,
    string? RecipientEmail,
    string? RecipientEmailMasked,
    string? ProjectTitle,
    string? ProjectCode,
    string? WorkItemTitle,
    string? WorkflowStageName,
    string? NotificationTitle,
    string? NotificationMessageSummary,
    string? DisplayMessageSummary,
    string DisplayTitle,
    string DisplayRecipient,
    string DisplayProject,
    string? DisplayRecipientKind,
    string? DisplayChannelTarget,
    string? ManualNotificationKind,
    string? ManualNotificationKindLabel,
    string? CorrelationId,
    string? LinkUrl,
    string AdminHandlingStatus,
    string AdminHandlingStatusLabel,
    DateTimeOffset? AdminHandledAtUtc,
    Guid? AdminHandledByUserId,
    string? AdminHandledByDisplayName,
    string? AdminHandlingNote,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record NotificationDeliveryAdminActionRequest(
    IReadOnlyList<Guid> Ids,
    string? Note);

public sealed record NotificationDeliveryAdminActionResponse(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    int SkippedCount,
    IReadOnlyList<NotificationDeliveryAdminActionItemResponse> Items);

public sealed record NotificationDeliveryAdminActionItemResponse(
    Guid DeliveryId,
    string Status,
    string Message);

public sealed record NotificationManualSendRequest(
    string? SendMode,
    string? NotificationKind,
    Guid? ProjectId,
    string? ProjectSelectionType,
    string? Title,
    string? ProjectName,
    string? Message,
    IReadOnlyList<string>? Channels,
    IReadOnlyList<Guid>? TeamsActivityRecipientUserIds,
    IReadOnlyList<Guid>? MailRecipientUserIds,
    IReadOnlyList<string>? MailRecipientEmails,
    IReadOnlyList<Guid>? WorkAssigneeUserIds,
    string? WorkflowStageCode,
    DateOnly? DueDate,
    Guid? TeamsActivityRecipientUserId,
    Guid? MailRecipientUserId,
    Guid? WorkAssigneeUserId,
    string? MailRecipientEmail,
    string? CorrelationId);

public sealed record NotificationManualSendResponse(
    string CorrelationId,
    int RequestedCount,
    int QueuedCount,
    IReadOnlyList<NotificationManualSendChannelResponse> Items);

public sealed record NotificationManualSendChannelResponse(
    string Channel,
    string ChannelLabel,
    Guid? DeliveryId,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string Target,
    string Message);

public sealed record NotificationDeliveryDetailResponse(
    Guid DeliveryId,
    string CategoryLabel,
    string? NotificationKindLabel,
    string? ProjectName,
    string Title,
    string? Message,
    DateTimeOffset? ManualRequestedAtUtc,
    DateTimeOffset CreatedAtUtc,
    string Channel,
    string ChannelLabel,
    string Recipient,
    string Status,
    string StatusLabel,
    int AttemptCount,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? SentAtUtc,
    string? ErrorCode,
    string? ErrorMessage,
    string ActionGuide,
    string AdminHandlingStatus,
    string AdminHandlingStatusLabel,
    string? AdminHandlingNote,
    string? CorrelationId,
    string? ProviderMessageId);

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

public sealed record NotificationTestTeamsActivityRequest(
    Guid? RecipientUserId,
    string? ActivityType,
    string? Title,
    string? Message,
    string? LinkUrl,
    string? InstalledAppId = null);

public sealed record NotificationTestTeamsActivityResponse(
    Guid DeliveryId,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string Provider,
    string CorrelationId,
    string ActivityType,
    string RecipientSource,
    string RecipientMasked,
    bool IsDryRun,
    bool IsActualEligible,
    string? ProviderMessageId);

public sealed record TeamsActivityRecipientProfile(
    Guid UserId,
    string DisplayName,
    string? Email,
    string? EntraObjectId,
    string AuthProvider,
    bool IsActive);
