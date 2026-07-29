namespace Emi.Qms.Api.PanelQr;

public sealed record PanelQrRecordResponse(
    Guid QrCodeId,
    Guid ProjectId,
    Guid PanelId,
    string Status,
    string ScanUrl,
    string IssuedByName,
    DateTimeOffset IssuedAtUtc);

public sealed record ProjectPanelQrItemResponse(
    Guid PanelId,
    int SequenceNumber,
    string DisplayCode,
    string DisplayName,
    bool QrEligible,
    bool HasActiveQr,
    PanelQrRecordResponse? Qr);

public sealed record ProjectPanelQrListResponse(
    Guid ProjectId,
    int EligibleCount,
    int IssuedCount,
    IReadOnlyList<ProjectPanelQrItemResponse> Panels);

public sealed record RotatePanelQrRequest(string? Reason);

public sealed record PanelQrBatchIssueRequest(IReadOnlyList<Guid>? PanelIds);

public sealed record PanelQrBatchIssueResponse(
    Guid ProjectId,
    int RequestedCount,
    int NewlyIssuedCount,
    int AlreadyIssuedCount);

public sealed record PanelQrPrintSheetRequest(IReadOnlyList<Guid>? PanelIds);

public sealed record PanelQrPrintSheetItemResponse(
    Guid PanelId,
    string DisplayCode,
    string DisplayName,
    string ImageUrl);

public sealed record PanelQrPrintSheetResponse(
    Guid ProjectId,
    int ItemCount,
    IReadOnlyList<PanelQrPrintSheetItemResponse> Items);

public sealed record PanelQrResolveRequest(string? Token);

public sealed record PanelQrResolveResponse(
    string Status,
    string Message,
    Guid? ProjectId = null,
    Guid? PanelId = null,
    string? ProjectCode = null,
    string? ProjectTitle = null,
    string? PanelDisplayName = null,
    string? CurrentStageCode = null,
    string? CurrentStageName = null,
    string? CurrentDepartmentCode = null,
    string? CurrentDepartmentName = null,
    bool CanEditCurrentStage = false,
    string? PrimaryActionLabel = null,
    string? PrimaryActionPath = null,
    string? OverviewPath = null);

public sealed record PanelQrImage(
    byte[] Content,
    string ContentType,
    string FileName);

public enum PanelQrMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    ValidationFailed,
    Conflict
}

public sealed record PanelQrMutationResult<T>(
    PanelQrMutationStatus Status,
    T? Value = default,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static PanelQrMutationResult<T> Success(T value) => new(PanelQrMutationStatus.Success, value);
    public static PanelQrMutationResult<T> NotFound() => new(PanelQrMutationStatus.NotFound);
    public static PanelQrMutationResult<T> Forbidden() => new(PanelQrMutationStatus.Forbidden);
    public static PanelQrMutationResult<T> Conflict(string message) => new(PanelQrMutationStatus.Conflict, Message: message);
    public static PanelQrMutationResult<T> Validation(string field, string message) =>
        new(PanelQrMutationStatus.ValidationFailed, Errors: new Dictionary<string, string[]> { [field] = [message] });
}

internal sealed record PanelQrSnapshot(
    Guid QrCodeId,
    Guid ProjectId,
    Guid PanelId,
    string Token,
    string Status,
    string IssuedByName,
    DateTimeOffset IssuedAtUtc,
    string ProjectKey,
    string ProjectCode,
    string ProjectTitle,
    string ProjectStatus,
    bool ProjectDeleted,
    string PanelStatus,
    string PanelDisplayCode,
    string PanelDisplayName);
