using Emi.Qms.Api.Pending;

namespace Emi.Qms.Api.QualityInspections;

public static class QualityInspectionStages
{
    public const string Lqc = "LQC";
    public const string ManufacturingCompleted = "ManufacturingCompleted";
    public const string Oqc = "OQC";
    public const string CustomerInspection = "CustomerInspection";
    public const string Fat = "FAT";

    public static readonly IReadOnlySet<string> InspectionStages = new HashSet<string>(StringComparer.Ordinal)
    {
        Lqc, Oqc, CustomerInspection, Fat
    };
}

public sealed record QualityInspectionQueueResponse(
    IReadOnlyList<QualityInspectionProjectResponse> Projects,
    bool IsOperational = true,
    string? OperationalMessage = null);

public sealed record QualityInspectionProjectResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    bool FatRequired,
    int ReadyCount,
    int InProgressCount,
    int BlockedCount,
    int CompletedCount,
    IReadOnlyList<QualityInspectionPanelSummary> Panels);

public sealed record QualityInspectionPanelSummary(
    Guid PanelId,
    string DisplayCode,
    string? PanelName,
    string WorkflowStage,
    string StageCode,
    string StageLabel,
    Guid WorkItemId,
    string WorkItemStatus,
    Guid? AttemptId,
    int AttemptNumber,
    string Status,
    int Version,
    Guid? PendingId,
    long? PendingNumber,
    string? ActionDepartmentCode,
    bool CanMutate);

public sealed record QualityInspectionDetailResponse(
    QualityInspectionPanelSummary Panel,
    string DecisionMode,
    Guid? ReportId,
    string? ReportStatus,
    int? ReportVersion,
    string? Result,
    string? Reason,
    string? PdfStatus,
    IReadOnlyList<QualityInspectionTemplateItemResponse> Items,
    IReadOnlyList<QualityInspectionItemValueResponse> Responses,
    IReadOnlyList<QualityInspectionPhotoResponse> Photos,
    IReadOnlyList<QualityInspectionAttemptHistoryResponse> History,
    ReinspectionEvidenceResponse? ReinspectionEvidence);

public sealed record QualityInspectionTemplateItemResponse(
    Guid ItemId,
    string ItemCode,
    int DisplayOrder,
    string Label,
    string? Guidance,
    string ResponseType,
    bool IsRequired,
    bool RequiresPhoto,
    int? MaxTextLength,
    bool IsAvailable = true,
    string? AvailabilityMessage = null,
    bool IsReinspectionTarget = false,
    string? PreviousFailureEvidence = null);

public sealed record QualityInspectionItemValueResponse(
    Guid TemplateItemId,
    string? CheckResult,
    string? TextValue,
    string? Note);

public sealed record QualityInspectionPhotoResponse(
    Guid PhotoId,
    Guid TemplateItemId,
    string DisplayName,
    string NormalizedMime,
    int ByteSize,
    string AltText,
    DateTimeOffset CreatedAtUtc);

public sealed record QualityInspectionAttemptHistoryResponse(
    Guid AttemptId,
    int AttemptNumber,
    string Status,
    Guid? PendingId,
    long? PendingNumber,
    DateTimeOffset? CompletedAtUtc);

public sealed record QualityActionDepartmentResponse(
    string DepartmentCode,
    string DepartmentName,
    IReadOnlyList<QualityActionOwnerResponse> Assignees);

public sealed record QualityActionOwnerResponse(Guid UserId, string DisplayName);

public sealed record StartQualityInspectionRequest(
    Guid OperationId,
    Guid ProjectId,
    Guid PanelId,
    string? StageCode);

public sealed record SaveQualityInspectionResponsesRequest(
    Guid OperationId,
    int? ExpectedReportVersion,
    IReadOnlyList<SaveQualityInspectionItemRequest>? Responses);

public sealed record SaveQualityInspectionItemRequest(
    Guid TemplateItemId,
    string? CheckResult,
    string? TextValue,
    string? Note);

public sealed record FinalizeQualityInspectionRequest(
    Guid OperationId,
    int? ExpectedReportVersion,
    string? Result,
    string? Reason,
    string? ActionDepartmentCode,
    Guid? AssigneeUserId,
    IReadOnlyList<SaveQualityInspectionItemRequest>? Responses = null);

public sealed record RequestQualityReinspectionRequest(
    Guid OperationId,
    Guid PendingId,
    int? ExpectedPendingVersion);

public sealed record ConfirmPanelManufacturingCompletedRequest(
    Guid OperationId,
    Guid ProjectId,
    Guid PanelId);

public sealed record QualityInspectionReconciliationResponse(
    int RecoveredLqcHandoffCount,
    int RecoveredOqcHandoffCount,
    int RecoveredInspectionHandoffCount,
    int RecoveredPackingHandoffCount,
    int UnresolvedAssigneeCount);

public sealed record RetryQualityInspectionPdfRequest(Guid OperationId);

public sealed record QualityInspectionMutationResponse(
    Guid OperationId,
    Guid ProjectId,
    Guid PanelId,
    string StageCode,
    Guid? AttemptId,
    Guid? ReportId,
    string Status,
    int Version,
    Guid? PendingId,
    long? PendingNumber,
    string? NextStageCode,
    bool Replayed);

public sealed record QualityInspectionPdfDownloadResult(string Status, byte[]? Content, string? ErrorCode);

public sealed record QualityInspectionPhotoContentResult(byte[] Content, string NormalizedMime, string DisplayName);

public enum QualityInspectionMutationStatus
{
    Success,
    NotFound,
    Validation,
    Conflict
}

public sealed class QualityInspectionMutationResult<T>
{
    private QualityInspectionMutationResult(
        QualityInspectionMutationStatus status,
        T? value = default,
        string? message = null,
        Dictionary<string, string[]>? errors = null)
    {
        Status = status;
        Value = value;
        Message = message;
        Errors = errors ?? [];
    }

    public QualityInspectionMutationStatus Status { get; }
    public T? Value { get; }
    public string? Message { get; }
    public Dictionary<string, string[]> Errors { get; }

    public static QualityInspectionMutationResult<T> Success(T value) => new(QualityInspectionMutationStatus.Success, value);
    public static QualityInspectionMutationResult<T> NotFound() => new(QualityInspectionMutationStatus.NotFound);
    public static QualityInspectionMutationResult<T> Conflict(string message) => new(QualityInspectionMutationStatus.Conflict, message: message);
    public static QualityInspectionMutationResult<T> Validation(Dictionary<string, string[]> errors) => new(QualityInspectionMutationStatus.Validation, errors: errors);
}
