namespace Emi.Qms.Api.OsanProjects;

public sealed record CreateOsanProjectRequest(
    string? Title,
    string? ProjectCode,
    string? CustomerName,
    string? PoNumber,
    string? WorkOrderNumber,
    DateOnly? DeliveryDate,
    string? ProductName,
    int? Quantity,
    Guid OperationId);

public sealed record OsanProjectListResponse(IReadOnlyList<OsanProjectListItemResponse> Items);

public sealed record OsanProjectListItemResponse(
    Guid ProjectId,
    string Title,
    string ProjectCode,
    string CustomerName,
    string? PoNumber,
    string? WorkOrderNumber,
    DateOnly DeliveryDate,
    string ProductName,
    int Quantity,
    string Status,
    int CompletedStepCount,
    int TotalStepCount,
    DateTimeOffset CreatedAtUtc);

public sealed record OsanProjectDetailResponse(
    Guid ProjectId,
    string Title,
    string ProjectCode,
    string CustomerName,
    string? PoNumber,
    string? WorkOrderNumber,
    DateOnly DeliveryDate,
    string ProductName,
    int Quantity,
    string Status,
    int CompletedStepCount,
    int TotalStepCount,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<OsanProjectTargetResponse> Targets);

public sealed record OsanProjectTargetResponse(
    Guid TargetId,
    int SequenceNumber,
    string DisplayName,
    string Status,
    IReadOnlyList<OsanProjectStepResponse> Steps);

public sealed record OsanProjectStepResponse(
    Guid StepId,
    int SequenceNumber,
    string StepCode,
    string StepName,
    string Status);

public sealed record OsanProjectCreateResponse(
    Guid OperationId,
    bool Replayed,
    OsanProjectDetailResponse Project);

public sealed record OsanProjectErrorResponse(
    string ErrorCode,
    string Message,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record NormalizedCreateOsanProjectInput(
    string Title,
    string ProjectCode,
    string CustomerName,
    string? PoNumber,
    string? WorkOrderNumber,
    DateOnly DeliveryDate,
    string ProductName,
    int Quantity,
    Guid OperationId);

public enum OsanProjectCreateStatus
{
    Success,
    ProjectCodeConflict,
    OperationConflict
}

public sealed record OsanProjectCreateResult(
    OsanProjectCreateStatus Status,
    OsanProjectCreateResponse? Value = null);

public sealed record OsanProjectAccessRecord(Guid ProjectId, string ProjectKey);
