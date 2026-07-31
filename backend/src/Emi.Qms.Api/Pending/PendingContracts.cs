namespace Emi.Qms.Api.Pending;

public static class PendingIssueTypes
{
    public const string Nonconformance = "Nonconformance";
    public const string Punch = "Punch";
    public const string ManufacturingStop = "ManufacturingStop";
    public const string Other = "Other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Nonconformance,
        Punch,
        ManufacturingStop,
        Other
    };
}

public static class PendingStatuses
{
    public const string Registered = "Registered";
    public const string ActionRequested = "ActionRequested";
    public const string InProgress = "InProgress";
    public const string ReinspectionRequested = "ReinspectionRequested";
    public const string Closed = "Closed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Registered,
        ActionRequested,
        InProgress,
        ReinspectionRequested,
        Closed
    };
}

public static class PendingPriorities
{
    public const string Normal = "Normal";
    public const string Urgent = "Urgent";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Normal,
        Urgent
    };
}

public sealed record CreatePendingRequest(
    Guid? ProjectId,
    string? IssueType,
    string? Title,
    string? Description,
    string? Priority,
    Guid? AssigneeUserId,
    DateOnly? DueDate,
    string? ActionDepartmentCode = null);

public sealed record TransitionPendingRequest(string? ToStatus, int? ExpectedVersion, string? Reason);

public sealed record AssignPendingRequest(Guid? AssigneeUserId, int? ExpectedVersion, string? Reason);

public sealed record AddPendingCommentRequest(string? Body);

public sealed record PendingListResponse(
    PendingSummaryResponse Summary,
    IReadOnlyList<PendingListItemResponse> Items);

public sealed record PendingSummaryResponse(
    int OpenCount,
    int UrgentCount,
    int OverdueCount,
    int ReinspectionCount,
    int ClosedCount);

public record PendingListItemResponse(
    Guid PendingId,
    long IssueNumber,
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    string TargetType,
    Guid TargetId,
    string? TargetLabel,
    string IssueType,
    string IssueTypeLabel,
    string Title,
    string Description,
    string Status,
    string StatusLabel,
    string Priority,
    string PriorityLabel,
    string? ActionDepartmentCode,
    Guid? AssigneeUserId,
    string? AssigneeDisplayName,
    DateOnly? DueDate,
    bool IsOverdue,
    int Version,
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PendingDetailResponse(
    PendingListItemResponse Issue,
    IReadOnlyList<PendingCommentResponse> Comments,
    IReadOnlyList<PendingHistoryResponse> History,
    IReadOnlyList<string> AllowedTransitions,
    bool CanComment,
    bool CanAssign,
    PendingReinspectionResponse? Reinspection,
    PendingActionEvidenceResponse ActionEvidence);

public sealed record PendingActionEvidenceResponse(
    bool CanManageDraft,
    int MaxPhotosPerRound,
    int MaxBytesPerRound,
    int MaxPhotosPerPending,
    int RemainingPhotosThisRound,
    int RemainingBytesThisRound,
    int RemainingPhotosForPending,
    IReadOnlyList<PendingActionPhotoResponse>? DraftPhotos,
    IReadOnlyList<PendingActionPhotoRoundResponse> ConfirmedRounds);

public sealed record PendingActionPhotoRoundResponse(
    int ActionRound,
    string ActionReasonSnapshot,
    Guid ConfirmedByUserId,
    string ConfirmedByDisplayName,
    DateTimeOffset ConfirmedAtUtc,
    IReadOnlyList<PendingActionPhotoResponse> Photos);

public sealed record PendingActionPhotoResponse(
    Guid PhotoId,
    string DisplayName,
    string NormalizedMime,
    int ByteSize,
    string AltText,
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    DateTimeOffset CreatedAtUtc);

public sealed record PendingPhotoMutationResponse(
    Guid OperationId,
    int ResultingPendingVersion,
    Guid? PhotoId,
    bool Replayed,
    PendingDetailResponse Detail);

public sealed record PendingPhotoOperationProjection(
    Guid OperationId,
    Guid PendingId,
    int ResultingPendingVersion,
    Guid? PhotoId,
    bool Replayed);

public sealed record PendingPhotoContentResult(
    byte[] Content,
    string NormalizedMime,
    string DisplayName);

public sealed record EvidencePhotoReferenceResponse(
    string SourceKind,
    Guid SourceId,
    Guid PhotoId,
    string DisplayName,
    string NormalizedMime,
    int ByteSize,
    string AltText);

public sealed record PendingActionRoundEvidenceResponse(
    int ActionRound,
    string ActionReasonSnapshot,
    string ConfirmedByDisplayName,
    DateTimeOffset ConfirmedAtUtc,
    IReadOnlyList<EvidencePhotoReferenceResponse> Photos);

public sealed record ReinspectionEvidenceResponse(
    IReadOnlyList<EvidencePhotoReferenceResponse> OriginalFailurePhotos,
    PendingActionRoundEvidenceResponse? LatestActionRound);

public sealed record PendingReinspectionResponse(
    Guid AttemptId,
    int AttemptNumber,
    string? OrderItem,
    decimal? Quantity,
    string? Unit,
    string LinkUrl);

public sealed record PendingCommentResponse(
    Guid CommentId,
    string Body,
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    DateTimeOffset CreatedAtUtc);

public sealed record PendingHistoryResponse(
    Guid HistoryId,
    string EventType,
    string EventLabel,
    string? FromStatus,
    string? FromStatusLabel,
    string? ToStatus,
    string? ToStatusLabel,
    string? FromAssigneeDisplayName,
    string? ToAssigneeDisplayName,
    string? Reason,
    Guid ChangedByUserId,
    string ChangedByDisplayName,
    DateTimeOffset CreatedAtUtc);

public sealed record PendingAssigneeResponse(Guid UserId, string DisplayName, string DepartmentCode);

public sealed record ManufacturingStopPendingResult(Guid PendingId, long IssueNumber);

public sealed record PanelQualityPendingResult(Guid PendingId, long IssueNumber);

public sealed record PendingActor(Guid UserId, bool IsCoordinator, bool IsQuality, bool CanComment);

public sealed record PendingMutationResult<T>(
    PendingMutationStatus Status,
    T? Value = default,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static PendingMutationResult<T> Success(T value) => new(PendingMutationStatus.Success, value);
    public static PendingMutationResult<T> NotFound() => new(PendingMutationStatus.NotFound);
    public static PendingMutationResult<T> Forbidden() => new(PendingMutationStatus.Forbidden);
    public static PendingMutationResult<T> Conflict(string message) => new(PendingMutationStatus.Conflict, Message: message);
    public static PendingMutationResult<T> Validation(IReadOnlyDictionary<string, string[]> errors) => new(PendingMutationStatus.Validation, Errors: errors);
}

public enum PendingMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    Validation
}
