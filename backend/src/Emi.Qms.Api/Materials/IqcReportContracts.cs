using Emi.Qms.Api.Pending;

namespace Emi.Qms.Api.Materials;

public static class IqcReportStatuses
{
    public const string Draft = "Draft";
    public const string Finalized = "Finalized";
}

public static class IqcPdfStatuses
{
    public const string Pending = "Pending";
    public const string Ready = "Ready";
    public const string Failed = "Failed";
}

public sealed record IqcTemplateItemResponse(
    Guid ItemId,
    string ItemCode,
    int DisplayOrder,
    string Label,
    string? Guidance,
    string ResponseType,
    bool IsRequired,
    bool RequiresPhoto,
    int? MaxTextLength);

public sealed record IqcItemResponseValue(
    Guid TemplateItemId,
    string? CheckResult,
    string? TextValue,
    string? Note);

public sealed record IqcPhotoResponse(
    Guid PhotoId,
    Guid TemplateItemId,
    string DisplayName,
    string NormalizedMime,
    int ByteSize,
    string AltText,
    DateTimeOffset CreatedAtUtc);

public sealed record IqcScanAttachmentResponse(
    Guid AttachmentId,
    string OriginalFileName,
    string NormalizedMime,
    int ByteSize,
    DateTimeOffset CreatedAtUtc);

public sealed record IqcScanAttemptHistoryResponse(
    Guid ReportId,
    int AttemptNumber,
    string Result,
    string Reason,
    string? ActionReason,
    DateTimeOffset FinalizedAtUtc,
    string? FinalizedBy,
    IReadOnlyList<IqcScanAttachmentResponse> Attachments);

public sealed record IqcReinspectionFailureResponse(
    string ItemCode,
    string Label,
    string? Note);

public sealed record IqcReinspectionSourceResponse(
    int PreviousAttemptNumber,
    string FailureReason,
    string? ActionReason,
    IReadOnlyList<IqcReinspectionFailureResponse> Failures,
    ReinspectionEvidenceResponse Evidence);

public sealed record IqcReportResponse(
    Guid AttemptId,
    Guid ReceiptId,
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    string? OrderItem,
    decimal? Quantity,
    string? Unit,
    int AttemptNumber,
    int ReceiptVersion,
    string AttemptStatus,
    string DecisionMode,
    Guid? ReportId,
    string? ReportStatus,
    int? ReportVersion,
    string? Result,
    string? Reason,
    string? PdfStatus,
    string? PdfErrorCode,
    int TemplateVersion,
    bool CanEdit,
    IqcReinspectionSourceResponse? ReinspectionSource,
    IReadOnlyList<IqcTemplateItemResponse> Items,
    IReadOnlyList<IqcItemResponseValue> Responses,
    IReadOnlyList<IqcPhotoResponse> Photos,
    DateTimeOffset? FinalizedAtUtc,
    string? FinalizedBy,
    IReadOnlyList<IqcScanAttachmentResponse> ScanAttachments,
    IReadOnlyList<IqcScanAttemptHistoryResponse> ScanHistory);

public sealed record SaveIqcResponsesRequest(
    int? ExpectedReportVersion,
    IReadOnlyList<SaveIqcItemResponseRequest>? Responses);

public sealed record SaveIqcItemResponseRequest(
    Guid TemplateItemId,
    string? CheckResult,
    string? TextValue,
    string? Note);

public sealed record FinalizeIqcReportRequest(
    int? ExpectedReportVersion,
    int? ExpectedReceiptVersion,
    string? Result,
    string? Reason);

public sealed record IqcPdfDownloadResult(
    string Status,
    byte[]? Content,
    string? ErrorCode);

public sealed record IqcPhotoContentResult(
    byte[] Content,
    string NormalizedMime,
    string DisplayName);
