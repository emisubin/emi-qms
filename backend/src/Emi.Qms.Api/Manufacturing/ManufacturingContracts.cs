namespace Emi.Qms.Api.Manufacturing;

public static class ManufacturingExecutionStatuses
{
    public const string InProgress = "InProgress";
    public const string Blocked = "Blocked";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class ManufacturingStopReasons
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "Material",
        "Staffing",
        "WorkUnavailable",
        "Other"
    };
}

public sealed record ManufacturingQueueResponse(IReadOnlyList<ManufacturingProjectResponse> Projects);

public sealed record ManufacturingReleaseQueueResponse(IReadOnlyList<ManufacturingReleaseProjectResponse> Projects);

public sealed record ManufacturingReleaseProjectResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    int ActiveItemCount,
    int CompletedItemCount,
    IReadOnlyList<ManufacturingReleasePanelResponse> Panels);

public sealed record ManufacturingReleasePanelResponse(
    Guid PanelId,
    string DisplayCode,
    string? PanelName,
    bool PanelInfoCompleted,
    bool KittingCompleted,
    bool Released,
    string? WorkItemStatus,
    DateTimeOffset? ReleasedAtUtc,
    bool Selectable);

public sealed record ManufacturingProjectResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    int ReadyCount,
    int InProgressCount,
    int BlockedCount,
    int CompletedCount,
    IReadOnlyList<ManufacturingPanelResponse> Panels);

public sealed record ManufacturingPanelResponse(
    Guid PanelId,
    string DisplayCode,
    string? PanelName,
    string WorkflowStage,
    bool KittingCompleted,
    Guid WorkItemId,
    string WorkItemStatus,
    Guid? ExecutionId,
    string Status,
    int Version,
    int CheckedStepCount,
    int TotalStepCount,
    Guid? ActivePendingId,
    long? ActivePendingNumber,
    string? ActionDepartmentCode,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool CanMutate,
    IReadOnlyList<ManufacturingBatchStepResponse> BatchSteps);

public sealed record ManufacturingBatchStepResponse(
    int SequenceNumber,
    string StepName,
    bool Checked);

public sealed record ManufacturingExecutionDetailResponse(
    ManufacturingPanelResponse Panel,
    IReadOnlyList<ManufacturingStepResponse> Steps,
    IReadOnlyList<ManufacturingEventResponse> Events);

public sealed record ManufacturingStepResponse(
    Guid StepId,
    int SequenceNumber,
    string StepName,
    bool Checked,
    string? CheckedByDisplayName,
    DateTimeOffset? CheckedAtUtc);

public sealed record ManufacturingEventResponse(
    Guid EventId,
    string EventType,
    string EventLabel,
    string? StopReasonCode,
    string? StopDescription,
    Guid? PendingId,
    string ActorDisplayName,
    DateTimeOffset? CreatedAtUtc);

public sealed record ManufacturingActionDepartmentResponse(
    string DepartmentCode,
    string DepartmentName,
    IReadOnlyList<ManufacturingActionOwnerResponse> Assignees);

public sealed record ManufacturingActionOwnerResponse(
    Guid UserId,
    string DisplayName);

public sealed record StartManufacturingRequest(Guid OperationId, Guid ProjectId, Guid PanelId);

public sealed record ReleaseManufacturingRequest(Guid OperationId, Guid ProjectId, IReadOnlyList<Guid>? PanelIds);

public sealed record ManufacturingReleaseResponse(
    Guid OperationId,
    int ReleasedPanelCount,
    int GeneratedWorkItemCount,
    bool Replayed);

public sealed record CheckManufacturingStepRequest(Guid OperationId, Guid StepId, int ExpectedVersion);

public sealed record StepBatchPanelRequest(Guid PanelId, Guid ExecutionId, int ExpectedVersion);

public sealed record StepBatchManufacturingRequest(
    Guid OperationId,
    Guid ProjectId,
    int TargetStepSequence,
    string? TargetStepName,
    IReadOnlyList<StepBatchPanelRequest>? Panels);

public sealed record StepBatchManufacturingResponse(
    Guid OperationId,
    Guid ProjectId,
    int CompletedPanelCount,
    int CheckedStepCount,
    bool Replayed);

public sealed record StopManufacturingRequest(
    Guid OperationId,
    string? ReasonCode,
    string? Description,
    string? ActionDepartmentCode,
    Guid? AssigneeUserId,
    int ExpectedVersion);

public sealed record ResumeManufacturingRequest(Guid OperationId, int ExpectedVersion);

public sealed record CompleteManufacturingRequest(Guid OperationId, int ExpectedVersion);

public sealed record ManufacturingMutationResponse(
    Guid OperationId,
    Guid ProjectId,
    Guid PanelId,
    Guid ExecutionId,
    string Status,
    int Version,
    int CheckedStepCount,
    int TotalStepCount,
    Guid? PendingId,
    long? PendingNumber,
    bool PanelLqcWorkCreated,
    bool ProjectManufacturingCompleted,
    bool Replayed);

public enum ManufacturingMutationStatus
{
    Success,
    NotFound,
    Validation,
    Conflict
}

public sealed class ManufacturingMutationResult<T>
{
    private ManufacturingMutationResult(
        ManufacturingMutationStatus status,
        T? value = default,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? message = null)
    {
        Status = status;
        Value = value;
        Errors = errors ?? new Dictionary<string, string[]>();
        Message = message;
    }

    public ManufacturingMutationStatus Status { get; }
    public T? Value { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public string? Message { get; }

    public static ManufacturingMutationResult<T> Success(T value) => new(ManufacturingMutationStatus.Success, value);
    public static ManufacturingMutationResult<T> NotFound() => new(ManufacturingMutationStatus.NotFound);
    public static ManufacturingMutationResult<T> Validation(IReadOnlyDictionary<string, string[]> errors) => new(ManufacturingMutationStatus.Validation, errors: errors);
    public static ManufacturingMutationResult<T> Conflict(string message) => new(ManufacturingMutationStatus.Conflict, message: message);
}
