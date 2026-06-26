using System.Text.Json.Serialization;

namespace Emi.Qms.Api.PanelInformation;

public sealed record PanelInformationBulkUpdateRequest(
    string? Reason,
    IReadOnlyList<PanelInformationUpdateItemRequest>? Panels);

public sealed record PanelInformationUpdateItemRequest(
    Guid? PanelId,
    int? ExpectedPanelInfoVersion,
    PanelInformationPanelNameUpdateRequest? PanelNameUpdate,
    PanelInformationSizeUpdateRequest? SizeUpdate);

public sealed record PanelInformationPanelNameUpdateRequest(
    bool IsChanged,
    string? Value);

public sealed record PanelInformationSizeUpdateRequest(
    bool IsChanged,
    bool Clear,
    string? InputUnit,
    decimal? Width,
    decimal? Height,
    decimal? Depth);

public sealed record PanelInformationExcelExpectedVersion(
    Guid PanelId,
    int ExpectedPanelInfoVersion);

public sealed class PanelInformationResponse
{
    public Guid ProjectId { get; init; }
    public string ProjectStatus { get; init; } = "";
    public string? PackagingMethod { get; init; }
    public int ActivePanelCount { get; init; }
    public int PanelInfoCompletedCount { get; init; }
    public int PanelInfoPendingCount { get; init; }
    public int QrEligibleCount { get; init; }
    public int DuplicatePanelNameGroupCount { get; init; }
    public bool ProjectPanelInformationCompleted { get; init; }
    public string? PanelInformationStatusMessage { get; init; }
    public IReadOnlyList<PanelInformationPanelResponse> Panels { get; init; } = [];
}

public sealed class PanelInformationPanelResponse
{
    public Guid PanelId { get; init; }
    public Guid ProjectId { get; init; }
    public int SequenceNumber { get; init; }
    public string PanelNumber { get; init; } = "";
    public string DisplayCode { get; init; } = "";
    public string? PanelName { get; init; }
    public string DisplayName { get; init; } = "";
    public decimal? WidthMm { get; init; }
    public decimal? HeightMm { get; init; }
    public decimal? DepthMm { get; init; }
    public string PanelStatus { get; init; } = "";
    public bool PanelInfoCompleted { get; init; }
    public bool QrEligible { get; init; }
    public bool HasDuplicateName { get; init; }
    public int DuplicateNameCount { get; init; }
    public int PanelInfoVersion { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? PanelInfoUpdatedAtUtc { get; init; }
    public Guid? PanelInfoUpdatedByUserId { get; init; }
    public string? PanelInfoUpdatedByUserName { get; init; }
}

public sealed record PanelInformationHistoryResponse(
    IReadOnlyList<PanelAuditEventResponse> AuditEvents,
    IReadOnlyList<PanelInformationExcelImportBatchResponse> ExcelImportBatches);

public sealed class PanelAuditEventResponse
{
    public Guid AuditEventId { get; init; }
    public string EntityType { get; init; } = "";
    public Guid EntityId { get; init; }
    public Guid ProjectId { get; init; }
    public string Action { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PanelNumber { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PanelDisplayName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FieldName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OldValue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NewValue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    public Guid? ChangedByUserId { get; init; }
    public string? ChangedByUserName { get; init; }
    public DateTimeOffset ChangedAtUtc { get; init; }
    public string CorrelationId { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputSource { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ImportBatchId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputUnit { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalInputValue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImportFileName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ImportUploadedAtUtc { get; init; }
}

public sealed record PanelInformationTemplateDownload(
    byte[] Content,
    string FileName,
    string ContentType);

public sealed class PanelInformationExcelImportBatchResponse
{
    public Guid ImportBatchId { get; init; }
    public Guid ProjectId { get; init; }
    public string OriginalFileName { get; init; } = "";
    public long FileSizeBytes { get; init; }
    public string FileSha256 { get; init; } = "";
    public string? InputUnit { get; init; }
    public int TotalRowCount { get; init; }
    public int NewPanelCount { get; init; }
    public int ChangedPanelCount { get; init; }
    public int UnchangedPanelCount { get; init; }
    public Guid? UploadedByUserId { get; init; }
    public string? UploadedByUserName { get; init; }
    public DateTimeOffset UploadedAtUtc { get; init; }
    public string? Reason { get; init; }
}

public sealed class PanelInformationExcelPreviewResponse
{
    public string FileSha256 { get; init; } = "";
    public string? ExpectedPackagingMethod { get; init; }
    public string ExpectedProjectStatus { get; init; } = "";
    public int TotalRows { get; init; }
    public int NewCount { get; init; }
    public int ChangedCount { get; init; }
    public int UnchangedCount { get; init; }
    public int ErrorCount { get; init; }
    public bool ReasonRequired { get; init; }
    public IReadOnlyList<PanelInformationExcelExpectedVersion> ExpectedPanelInfoVersions { get; init; } = [];
    public IReadOnlyList<PanelInformationExcelPreviewRowResponse> Rows { get; init; } = [];
}

public sealed class PanelInformationExcelPreviewRowResponse
{
    public int ExcelRowNumber { get; init; }
    public int? No { get; init; }
    public Guid? PanelId { get; init; }
    public string? PanelName { get; init; }
    public decimal? Width { get; init; }
    public decimal? Height { get; init; }
    public decimal? Depth { get; init; }
    public decimal? WidthMm { get; init; }
    public decimal? HeightMm { get; init; }
    public decimal? DepthMm { get; init; }
    public PanelInformationPanelResponse? CurrentValue { get; init; }
    public string ResultType { get; init; } = "";
    public IReadOnlyList<string> ErrorMessages { get; init; } = [];
    public int? ExpectedPanelInfoVersion { get; init; }
}

public sealed record UploadedExcelFile(
    string OriginalFileName,
    long FileSizeBytes,
    string FileSha256,
    byte[] Content);

public enum PanelInformationMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    ValidationFailed
}

public sealed record PanelInformationMutationResult<T>(
    PanelInformationMutationStatus Status,
    T? Value = default,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static PanelInformationMutationResult<T> Success(T value)
    {
        return new PanelInformationMutationResult<T>(PanelInformationMutationStatus.Success, value);
    }

    public static PanelInformationMutationResult<T> NotFound()
    {
        return new PanelInformationMutationResult<T>(PanelInformationMutationStatus.NotFound);
    }

    public static PanelInformationMutationResult<T> Forbidden(string message)
    {
        return new PanelInformationMutationResult<T>(PanelInformationMutationStatus.Forbidden, Message: message);
    }

    public static PanelInformationMutationResult<T> Conflict(string message)
    {
        return new PanelInformationMutationResult<T>(PanelInformationMutationStatus.Conflict, Message: message);
    }

    public static PanelInformationMutationResult<T> Validation(IReadOnlyDictionary<string, string[]> errors)
    {
        return new PanelInformationMutationResult<T>(PanelInformationMutationStatus.ValidationFailed, Errors: errors);
    }
}
