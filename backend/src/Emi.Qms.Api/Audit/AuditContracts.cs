namespace Emi.Qms.Api.Audit;

public static class AuditEventTypes
{
    public const string Login = "Login";
    public const string Logout = "Logout";
    public const string MutationSucceeded = "MutationSucceeded";
    public const string MutationFailed = "MutationFailed";
    public const string AuthorizationDenied = "AuthorizationDenied";
}

public static class AuditFailureReasons
{
    public const string Validation = "Validation";
    public const string Conflict = "Conflict";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Validation, Conflict
    };
}

public sealed record RecordInteractiveLoginRequest(Guid ClientInteractionId);

public sealed record RecordAuditLogoutRequest(Guid LoginCorrelationId, Guid IdempotencyReceipt);

public sealed record AuditSessionResponse(
    Guid EventId,
    Guid LoginCorrelationId,
    Guid IdempotencyReceipt);

public sealed record AuditCoverageResponse(DateTimeOffset CoverageStartedAtUtc, string CompletenessNotice);

public sealed record AuditSummaryResponse(
    int TotalEvents,
    int LoginEvents,
    int SuccessfulChanges,
    int FailedChanges,
    int AuthorizationDenials);

public sealed record AuditListItemResponse(
    Guid EventId,
    string Source,
    DateTimeOffset OccurredAtUtc,
    string EventType,
    Guid? ActorUserId,
    string ActorDisplayName,
    string? ActorDepartmentName,
    Guid? ActualActorUserId,
    string? ActualActorDisplayName,
    string Domain,
    string Action,
    string? TargetType,
    string? TargetKey,
    string Outcome,
    string? FailureReason,
    string? ReasonSummary,
    Guid? LoginCorrelationId,
    int ChangeCount,
    string? ClientIp,
    string? BrowserFamily,
    string? OsFamily,
    string? AppAccessOutcome);

public sealed record AuditListResponse(
    IReadOnlyList<AuditListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    AuditSummaryResponse Summary,
    AuditCoverageResponse Coverage,
    DateOnly FromDate,
    DateOnly ToDate);

public sealed record AuditChangeResponse(
    long ChangeId,
    string RowAction,
    string TargetType,
    string TargetKey,
    string FieldCode,
    string ProjectionKind,
    string? BeforeValue,
    string? AfterValue,
    int? BeforeLength,
    int? AfterLength);

public sealed record AuditLoginContextResponse(
    DateTimeOffset OccurredAtUtc,
    string? ClientIp,
    string? BrowserFamily,
    string? OsFamily,
    string AppAccessOutcome);

public sealed record AuditDetailResponse(
    AuditListItemResponse Event,
    IReadOnlyList<AuditChangeResponse> Changes,
    AuditLoginContextResponse? LoginContext,
    string ValueNotice);

public sealed record AuditQuery(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    Guid? ActorUserId,
    string? Domain,
    string? Action,
    string? EventType,
    string? FailureReason,
    string? Search,
    int Page,
    int PageSize);
