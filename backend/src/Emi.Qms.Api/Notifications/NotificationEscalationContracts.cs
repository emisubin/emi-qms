namespace Emi.Qms.Api.Notifications;

public static class WorkItemEscalationLevels
{
    public const string None = "None";
    public const string L0 = "L0";
    public const string L1 = "L1";
    public const string L2 = "L2";
    public const string L3 = "L3";
}

public static class WorkItemEscalationStatuses
{
    public const string Active = "Active";
    public const string Resolved = "Resolved";
    public const string Cancelled = "Cancelled";
}

public sealed record NotificationEscalationSummary(
    int EvaluatedWorkItemCount,
    int CreatedNotificationCount,
    int CreatedDeliveryCount,
    int ResolvedEscalationCount);

public sealed record WorkItemEscalationCandidate(
    Guid WorkItemId,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    string WorkflowStageCode,
    string WorkflowStageName,
    string ResponsibilityType,
    Guid AssignedUserId,
    string AssignedDisplayName,
    bool AssignedUserIsActive,
    DateOnly DueDate,
    string WorkItemTitle,
    string WorkItemStatus,
    string? CurrentLevel,
    DateTimeOffset? L0SentAtUtc,
    DateTimeOffset? L1SentAtUtc,
    DateTimeOffset? L2SentAtUtc,
    DateTimeOffset? L3SentAtUtc);

public sealed record EscalationRecipient(Guid UserId, string DisplayName, string? Email);

public sealed record EscalationCreateResult(int NotificationCount, int DeliveryCount);

public sealed record TeamsPersonalDeliveryPlan(
    string Channel,
    string Status,
    string? ProviderMessageId);

public sealed record WorkItemEscalationListResponse(IReadOnlyList<WorkItemEscalationResponse> Items);

public sealed record WorkItemEscalationResponse(
    Guid EscalationId,
    Guid WorkItemId,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    string WorkflowStageCode,
    string WorkflowStageName,
    string WorkItemTitle,
    DateOnly DueDate,
    string Status,
    string CurrentLevel,
    DateTimeOffset? LastEscalatedAtUtc,
    DateTimeOffset? NextCheckAtUtc,
    DateTimeOffset? L0SentAtUtc,
    DateTimeOffset? L1SentAtUtc,
    DateTimeOffset? L2SentAtUtc,
    DateTimeOffset? L3SentAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    string? AssignedDisplayName,
    string? DeliveryStatusSummary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
