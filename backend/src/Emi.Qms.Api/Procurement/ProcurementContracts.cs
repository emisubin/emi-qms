using System.Text.Json.Serialization;

namespace Emi.Qms.Api.Procurement;

public sealed record ProcurementBulkUpdateRequest(
    string? Reason,
    IReadOnlyList<ProcurementItemUpdateRequest>? Items);

public sealed record ProcurementItemUpdateRequest(
    Guid? ItemId,
    int? ExpectedRowVersion,
    string? StandardLeadTime,
    string? OrderItem,
    string? TechnicalOwner,
    DateOnly? OrderDate,
    DateOnly? ExpectedReceiptDate,
    string? IssueNote,
    bool? ReceiptCompleted,
    DateTimeOffset? ReceiptCompletedAtUtc,
    string? ReceiptCompletionNote);

public sealed record ProcurementReceiptBulkUpdateRequest(
    string? Reason,
    IReadOnlyList<ProcurementReceiptUpdateRequest>? Items);

public sealed record ProcurementReceiptUpdateRequest(
    Guid? ItemId,
    int? ExpectedRowVersion,
    bool? ReceiptCompleted,
    DateTimeOffset? ReceiptCompletedAtUtc,
    string? ReceiptCompletionNote);

public sealed record ProcurementExcelProjectSelection(
    int SourceGroupSequence,
    Guid ProjectId);

public sealed record ProcurementExcelExpectedVersion(
    Guid ItemId,
    int ExpectedRowVersion);

public sealed record ProcurementResponse(
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    DateOnly? ProjectDeliveryDate,
    IReadOnlyList<ProcurementItemResponse> Items);

public sealed class ProcurementItemResponse
{
    public Guid ItemId { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectTitle { get; init; } = "";
    public string ProjectCode { get; init; } = "";
    public DateOnly? ProjectDeliveryDate { get; init; }
    public DateOnly? ShipmentDisplayDate { get; init; }
    public int SequenceNumber { get; init; }
    public string? SourceProjectText { get; init; }
    public string? SourceProjectCodeText { get; init; }
    public string? StandardLeadTime { get; init; }
    public string? OrderItem { get; init; }
    public string? TechnicalOwner { get; init; }
    public DateOnly? OrderDate { get; init; }
    public DateOnly? ExpectedReceiptDate { get; init; }
    public string? IssueNote { get; init; }
    public bool ReceiptCompleted { get; init; }
    public DateTimeOffset? ReceiptCompletedAtUtc { get; init; }
    public Guid? ReceiptCompletedByUserId { get; init; }
    public string? ReceiptCompletedByUserName { get; init; }
    public string? ReceiptCompletionNote { get; init; }
    public int RowVersion { get; init; }
    public string DDayText { get; init; } = "-";
}

public sealed record ProcurementListResponse(
    IReadOnlyList<ProcurementItemResponse> Items);

public sealed record ProcurementDashboardResponse(
    ProcurementDashboardSummaryResponse Summary,
    IReadOnlyList<ProcurementProjectSummaryResponse> Projects);

public sealed record ProcurementDashboardSummaryResponse(
    int PendingReceiptCount,
    int ReceiptCompletedCount,
    int PastExpectedReceiptDateCount);

public sealed class ProcurementProjectSummaryResponse
{
    public Guid ProjectId { get; init; }
    public string ProjectTitle { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string ProjectCode { get; init; } = "";
    public string Item { get; init; } = "";
    public int ActivePanelCount { get; init; }
    public DateOnly? DeliveryDate { get; init; }
    public int ProcurementItemCount { get; init; }
    public int ReceiptCompletedCount { get; init; }
    public int PastExpectedReceiptDateCount { get; init; }
    public DateOnly? NearestExpectedReceiptDate { get; init; }
    public string DDayText { get; init; } = "-";
}

public sealed record ProcurementTemplateDownload(
    byte[] Content,
    string FileName,
    string ContentType);

public sealed record ProcurementRequiredItemSettingsResponse(
    string ItemCode,
    Guid? ActiveTemplateId,
    int? ActiveTemplateVersion,
    IReadOnlyList<ProcurementRequiredItemSettingsRowResponse> Rows);

public sealed record ProcurementRequiredItemSettingsRowResponse(
    Guid? TemplateRowId,
    int SequenceNumber,
    string ItemName,
    bool IsRequired,
    bool IsActive);

public sealed record UpdateProcurementRequiredItemSettingsRequest(
    IReadOnlyList<UpdateProcurementRequiredItemSettingsRowRequest>? Rows,
    string? Reason);

public sealed record UpdateProcurementRequiredItemSettingsRowRequest(
    Guid? TemplateRowId,
    int? SequenceNumber,
    string? ItemName,
    bool? IsRequired,
    bool? IsActive);

public sealed class ProcurementExcelPreviewResponse
{
    public string FileSha256 { get; init; } = "";
    public int TotalRows { get; init; }
    public int NewCount { get; init; }
    public int ChangedCount { get; init; }
    public int UnchangedCount { get; init; }
    public int SkippedCount { get; init; }
    public int MissingFromUploadCount { get; init; }
    public int NeedsReviewCount { get; init; }
    public int ErrorCount { get; init; }
    public bool ReasonRequired { get; init; }
    public IReadOnlyList<ProcurementExcelProjectMatchResponse> ProjectMatches { get; init; } = [];
    public IReadOnlyList<ProcurementExcelPreviewRowResponse> Rows { get; init; } = [];
    public IReadOnlyList<ProcurementExcelExpectedVersion> ExpectedVersions { get; init; } = [];
}

public sealed class ProcurementExcelProjectMatchResponse
{
    public int SourceGroupSequence { get; init; }
    public string? ExcelProjectTitle { get; init; }
    public string? ExcelProjectCode { get; init; }
    public Guid? MatchedProjectId { get; init; }
    public string? MatchedProjectTitle { get; init; }
    public string? MatchedProjectCode { get; init; }
    public string MatchStatus { get; init; } = "";
    public IReadOnlyList<ProcurementProjectCandidateResponse> Candidates { get; init; } = [];
}

public sealed record ProcurementProjectCandidateResponse(
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    string MatchType);

public sealed class ProcurementExcelPreviewRowResponse
{
    public int ExcelRowNumber { get; init; }
    public int SourceGroupSequence { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ItemId { get; init; }
    public int? ExpectedRowVersion { get; init; }
    public string ResultType { get; init; } = "";
    public string? SourceProjectText { get; init; }
    public string? SourceProjectCodeText { get; init; }
    public string? StandardLeadTime { get; init; }
    public string? OrderItem { get; init; }
    public string? TechnicalOwner { get; init; }
    public DateOnly? OrderDate { get; init; }
    public DateOnly? ExpectedReceiptDate { get; init; }
    public string? ShipmentText { get; init; }
    public string? IssueNote { get; init; }
    public bool? ReceiptCompleted { get; init; }
    public IReadOnlyList<string> ErrorMessages { get; init; } = [];
}

public sealed record ProcurementHistoryResponse(
    IReadOnlyList<ProcurementHistoryGroupResponse> Groups,
    IReadOnlyList<ProcurementExcelImportBatchResponse> ExcelImportBatches);

public sealed class ProcurementHistoryGroupResponse
{
    public string GroupId { get; init; } = "";
    public string InputSource { get; init; } = "";
    public Guid? ChangedByUserId { get; init; }
    public string? ChangedByName { get; init; }
    public DateTimeOffset ChangedAtUtc { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ImportBatchId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImportFileName { get; init; }

    public int AffectedItemCount { get; init; }
    public int ChangeCount { get; init; }
    public IReadOnlyList<ProcurementHistoryChangeResponse> Changes { get; init; } = [];
}

public sealed class ProcurementHistoryChangeResponse
{
    public Guid EntityId { get; init; }
    public int? SequenceNumber { get; init; }
    public string? FieldName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

public sealed class ProcurementExcelImportBatchResponse
{
    public Guid ImportBatchId { get; init; }
    public string OriginalFileName { get; init; } = "";
    public long FileSizeBytes { get; init; }
    public string FileSha256 { get; init; } = "";
    public int TotalRowCount { get; init; }
    public int NewItemCount { get; init; }
    public int ChangedItemCount { get; init; }
    public int UnchangedItemCount { get; init; }
    public int SkippedItemCount { get; init; }
    public int MissingFromUploadCount { get; init; }
    public Guid? UploadedByUserId { get; init; }
    public string? UploadedByUserName { get; init; }
    public DateTimeOffset UploadedAtUtc { get; init; }
    public string? Reason { get; init; }
}

public sealed record ProcurementMutationResult<T>(
    ProcurementMutationStatus Status,
    T? Value,
    IReadOnlyDictionary<string, string[]> Errors,
    string? Message)
{
    public static ProcurementMutationResult<T> Success(T value) => new(ProcurementMutationStatus.Success, value, new Dictionary<string, string[]>(), null);
    public static ProcurementMutationResult<T> Validation(IReadOnlyDictionary<string, string[]> errors) => new(ProcurementMutationStatus.Validation, default, errors, null);
    public static ProcurementMutationResult<T> NotFound() => new(ProcurementMutationStatus.NotFound, default, new Dictionary<string, string[]>(), null);
    public static ProcurementMutationResult<T> Forbidden() => new(ProcurementMutationStatus.Forbidden, default, new Dictionary<string, string[]>(), null);
    public static ProcurementMutationResult<T> Conflict(string message) => new(ProcurementMutationStatus.Conflict, default, new Dictionary<string, string[]>(), message);
}

public enum ProcurementMutationStatus
{
    Success,
    Validation,
    NotFound,
    Forbidden,
    Conflict
}
