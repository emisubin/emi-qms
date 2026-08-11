namespace Emi.Qms.Api.Notices;

public sealed record NoticeListResponse(
    IReadOnlyList<NoticeListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record NoticeListItemResponse(
    Guid NoticeId,
    string Title,
    string Preview,
    string AuthorDisplayName,
    string? AuthorDepartmentName,
    DateTimeOffset CreatedAtUtc,
    bool CanDelete,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record NoticeDetailResponse(
    Guid NoticeId,
    string Title,
    string Body,
    string BodyFormat,
    int Version,
    string AuthorDisplayName,
    string? AuthorDepartmentName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    bool CanEdit,
    bool CanDelete,
    IReadOnlyList<NoticeAttachmentResponse> Attachments);

public sealed record NoticeAttachmentResponse(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    int ByteSize,
    DateTimeOffset CreatedAtUtc,
    bool CanDelete);

public sealed record NoticeAttachmentContentResult(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record NoticeDeleteResponse(Guid NoticeId, bool Deleted);

public sealed record NoticeAttachmentDeleteResponse(Guid NoticeId, Guid AttachmentId, bool Deleted);

public sealed record CreateNoticeRequest(
    Guid? RequestId,
    string? Title,
    string? Body,
    string? BodyFormat = NoticeBodyFormats.BoldMarkupV1);

public sealed record UpdateNoticeRequest(
    int? ExpectedVersion,
    string? Title,
    string? Body,
    string? BodyFormat);

public static class NoticeBodyFormats
{
    public const string PlainTextV1 = "PlainTextV1";
    public const string BoldMarkupV1 = "BoldMarkupV1";
}

public sealed record NoticeMutationResult<T>(
    NoticeMutationStatus Status,
    T? Value = default,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static NoticeMutationResult<T> Success(T value) => new(NoticeMutationStatus.Success, value);
    public static NoticeMutationResult<T> NotFound() => new(NoticeMutationStatus.NotFound);
    public static NoticeMutationResult<T> Forbidden() => new(NoticeMutationStatus.Forbidden);
    public static NoticeMutationResult<T> Conflict(string message) => new(NoticeMutationStatus.Conflict, Message: message);
    public static NoticeMutationResult<T> Validation(IReadOnlyDictionary<string, string[]> errors)
        => new(NoticeMutationStatus.Validation, Errors: errors);
}

public enum NoticeMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    Validation
}
