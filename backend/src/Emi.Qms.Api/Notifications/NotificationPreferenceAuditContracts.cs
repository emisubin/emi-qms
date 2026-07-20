namespace Emi.Qms.Api.Notifications;

public sealed record NotificationPreferenceAuditSummaryResponse(
    int TotalChanges,
    int UserChanges,
    int AdminChanges,
    int TurnedOffChanges);

public sealed record NotificationPreferenceAuditItemResponse(
    Guid AuditEventId,
    DateTimeOffset OccurredAtUtc,
    string TargetDisplayName,
    string? TargetDepartmentName,
    bool TargetIsActive,
    string ActorDisplayName,
    string? ActorDepartmentName,
    bool ActorIsActive,
    string Action,
    string ActionLabel,
    string DeliveryType,
    string DeliveryTypeLabel,
    string Channel,
    string ChannelLabel,
    bool OldValue,
    bool NewValue,
    string ChangeLabel,
    long ResultingVersion);

public sealed record NotificationPreferenceAuditListResponse(
    IReadOnlyList<NotificationPreferenceAuditItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    NotificationPreferenceAuditSummaryResponse Summary,
    string IdentityNotice,
    DateOnly FromDate,
    DateOnly ToDate);
