namespace Emi.Qms.Api.Workflow;

public static class WorkflowStageCodes
{
    public const string SalesProjectCreated = "SalesProjectCreated";
    public const string DesignPanelInfo = "DesignPanelInfo";
    public const string ProductionPlanning = "ProductionPlanning";
    public const string ProcurementInfo = "ProcurementInfo";
    public const string MaterialArrived = "MaterialArrived";
    public const string IQC = "IQC";
    public const string ReceiptConfirmed = "ReceiptConfirmed";
    public const string KittingCompleted = "KittingCompleted";
    public const string ManufacturingWork = "ManufacturingWork";
    public const string LQC = "LQC";
    public const string ManufacturingCompleted = "ManufacturingCompleted";
    public const string OQC = "OQC";
    public const string CustomerInspection = "CustomerInspection";
    public const string FAT = "FAT";
    public const string PackingCompleted = "PackingCompleted";
    public const string DepartureProcessed = "DepartureProcessed";
    public const string DeliveryCompleted = "DeliveryCompleted";
    public const string SalesSettlementCompleted = "SalesSettlementCompleted";
}

public static class WorkflowStatuses
{
    public const string Requested = "Requested";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public sealed record WorkflowStageResponse(
    string StageCode,
    int SequenceNumber,
    string DepartmentCode,
    string DepartmentLabel,
    string StageName,
    bool IsOptional,
    bool IsActive);

public sealed record ProjectWorkflowResponse(
    Guid ProjectId,
    IReadOnlyList<ProjectWorkflowStageResponse> Stages,
    int GeneratedWorkItemCount,
    int RequiredStageCount,
    int CompletedRequiredStageCount,
    int ProgressPercent,
    string CurrentStageCode,
    string CurrentStageName,
    string CurrentDepartmentCode,
    string CurrentDepartmentLabel);

public sealed record ProjectWorkflowStageResponse(
    string StageCode,
    int SequenceNumber,
    string DepartmentCode,
    string DepartmentLabel,
    string StageName,
    bool IsOptional,
    string Status,
    string StatusLabel,
    int WorkItemCount,
    DateTimeOffset? CompletedAtUtc);

public sealed record MyWorkSummaryResponse(
    int RequestedCount,
    int InProgressCount,
    int CompletedCount,
    int BlockingCount,
    int AssignedProjectCount,
    IReadOnlyList<MyAssignedProjectBreakdownResponse> AssignedProjectBreakdown);

public sealed record MyAssignedProjectBreakdownResponse(
    string ResponsibilityType,
    string ResponsibilityLabel,
    int ProjectCount);

public sealed record MyWorkListResponse(IReadOnlyList<MyWorkItemResponse> Items);

public sealed record MyAssignedProjectsResponse(IReadOnlyList<MyAssignedProjectResponse> Items);

public sealed record MyAssignedProjectResponse(
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    string Item,
    DateOnly? DeliveryDate,
    string ProjectStatus,
    string ProjectStatusLabel,
    IReadOnlyList<MyAssignedProjectResponsibilityResponse> Responsibilities);

public sealed record MyAssignedProjectResponsibilityResponse(
    string ResponsibilityType,
    string ResponsibilityLabel);

public sealed record MyWorkItemResponse(
    Guid WorkItemId,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    string ProjectItem,
    DateOnly? ProjectDeliveryDate,
    string WorkflowStageCode,
    string WorkflowStageName,
    string ResponsibilityType,
    string ResponsibilityLabel,
    string Title,
    string? Description,
    string Status,
    string StatusLabel,
    string Priority,
    string PriorityLabel,
    DateOnly? DueDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string LinkUrl);

public sealed record NotificationSummaryResponse(
    int UnreadCount,
    int BlockingCount);

public sealed record NotificationListResponse(IReadOnlyList<NotificationResponse> Items);

public sealed record NotificationResponse(
    Guid NotificationId,
    Guid? ProjectId,
    string? ProjectTitle,
    string? ProjectCode,
    string? ProjectItem,
    Guid? WorkItemId,
    string? WorkItemTitle,
    string? WorkflowStageCode,
    string? WorkflowStageName,
    string NotificationType,
    string NotificationTypeLabel,
    string Severity,
    string SeverityLabel,
    string VisibilityScope,
    string VisibilityScopeLabel,
    string SourceKind,
    string SourceKindLabel,
    string Title,
    string Message,
    string? LinkUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);

public sealed record WorkflowMutationResult<T>(
    WorkflowMutationStatus Status,
    T? Value = default,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static WorkflowMutationResult<T> Success(T value)
    {
        return new WorkflowMutationResult<T>(WorkflowMutationStatus.Success, value);
    }

    public static WorkflowMutationResult<T> NotFound()
    {
        return new WorkflowMutationResult<T>(WorkflowMutationStatus.NotFound);
    }

    public static WorkflowMutationResult<T> Forbidden()
    {
        return new WorkflowMutationResult<T>(WorkflowMutationStatus.Forbidden);
    }

    public static WorkflowMutationResult<T> Conflict(string message)
    {
        return new WorkflowMutationResult<T>(WorkflowMutationStatus.Conflict, Message: message);
    }
}

public enum WorkflowMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}
