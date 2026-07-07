namespace Emi.Qms.Api.Admin;

public sealed record AdminDashboardResponse(
    int PendingUserCount,
    int FailedDeliveryCount,
    int PendingDeliveryCount,
    DateTimeOffset? LastDailyDigestSentAtUtc,
    int ActiveEscalationCount,
    int RecentMasterChangeCount,
    IReadOnlyList<AdminDashboardEscalationLevelResponse> ActiveEscalationLevels);

public sealed record AdminDashboardEscalationLevelResponse(
    string Level,
    string Label,
    int Count);

public sealed record AdminDepartmentListResponse(IReadOnlyList<AdminDepartmentMasterResponse> Departments);

public sealed record AdminDepartmentMasterResponse(
    Guid DepartmentId,
    string Code,
    string Name,
    bool IsActive,
    int SortOrder,
    int UserCount,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? DeletionRequestedAtUtc,
    DateTimeOffset? ScheduledHardDeleteAtUtc,
    DateTimeOffset? PurgeBlockedAtUtc,
    string? PurgeBlockedReason,
    bool? PreDeleteIsActive)
{
    public string LifecycleStatus => AdminDeletionLifecycle.Calculate(
        IsActive,
        DeletionRequestedAtUtc,
        ScheduledHardDeleteAtUtc,
        PurgeBlockedAtUtc);

    public string LifecycleStatusLabel => AdminDeletionLifecycle.Label(LifecycleStatus);

    public string? ScheduledHardDeleteLabel => AdminDeletionLifecycle.FormatScheduledHardDeleteLabel(ScheduledHardDeleteAtUtc);
}

public sealed record CreateAdminDepartmentRequest(
    string? Code,
    string? Name,
    bool? IsActive,
    int? SortOrder,
    string? Reason);

public sealed record UpdateAdminDepartmentRequest(
    string? Name,
    bool? IsActive,
    int? SortOrder,
    string? Reason);

public sealed record AdminReorderRequest(IReadOnlyList<AdminReorderItem> Items, string? Reason);

public sealed record AdminReorderItem(Guid Id, int SortOrder);

public sealed record AdminBulkActionRequest(IReadOnlyList<Guid> Ids, string? Reason);

public sealed record AdminBulkActionResponse(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    int SkippedCount,
    IReadOnlyList<AdminBulkActionItemResponse> Items);

public sealed record AdminBulkActionItemResponse(
    Guid Id,
    string Status,
    string Message);

public sealed record PermissionMatrixResponse(
    IReadOnlyList<PermissionMatrixRoleResponse> Roles,
    IReadOnlyList<PermissionMatrixPermissionResponse> Permissions,
    IReadOnlyList<PermissionMatrixAssignmentResponse> Assignments);

public sealed record PermissionMatrixRoleResponse(Guid RoleId, string Code, string Name);

public sealed record PermissionMatrixPermissionResponse(Guid PermissionId, string Code, string Name);

public sealed record PermissionMatrixAssignmentResponse(Guid RoleId, Guid PermissionId);

public sealed record AdminMasterChangeLogListResponse(IReadOnlyList<AdminMasterChangeLogResponse> Items);

public sealed record AdminMasterChangeLogResponse(
    Guid ChangeLogId,
    string EntityType,
    Guid? EntityId,
    string Action,
    string? BeforeJson,
    string? AfterJson,
    string? Reason,
    Guid? ChangedByUserId,
    string? ChangedByDisplayName,
    DateTimeOffset ChangedAtUtc);

public sealed record AdminWorkItemHistoryListResponse(IReadOnlyList<AdminWorkItemHistoryResponse> Items);

public sealed record AdminWorkItemHistoryResponse(
    Guid WorkItemId,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    string WorkflowStageCode,
    string WorkflowStageName,
    string Title,
    string Status,
    Guid? AssignedUserId,
    string? AssignedDisplayName,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateOnly? DueDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminMasterMutationResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorMessage,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors)
{
    public static AdminMasterMutationResult<T> Success(T value) => new(true, value, null, new Dictionary<string, IReadOnlyList<string>>());
    public static AdminMasterMutationResult<T> Failure(string message) => new(false, default, message, new Dictionary<string, IReadOnlyList<string>>());
    public static AdminMasterMutationResult<T> ValidationFailure(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        new(false, default, "입력값을 확인해주세요.", fieldErrors);
}
