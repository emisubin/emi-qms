using System.Text.Json.Serialization;

namespace Emi.Qms.Api.Projects;

public sealed record CreateProjectRequest(
    string? CustomerName,
    string? Item,
    string? ProjectCode,
    string? ProjectTitle,
    int? PanelCount,
    DateOnly? DeliveryDate,
    Guid? SalesOwnerUserId,
    string? PackagingMethod,
    decimal? SalesAmount,
    string? CurrencyCode,
    string? DeliveryLocation);

public sealed record UpdateProjectRequest(
    string? CustomerName,
    string? Item,
    string? ProjectCode,
    string? ProjectTitle,
    DateOnly? DeliveryDate,
    Guid? SalesOwnerUserId,
    string? PackagingMethod,
    decimal? SalesAmount,
    string? CurrencyCode,
    string? DeliveryLocation,
    string? Reason);

public sealed record ChangePanelCountRequest(
    int? PanelCount,
    int? ExpectedActivePanelCount,
    IReadOnlyList<Guid>? CancelPanelIds,
    string? Reason);

public sealed record ProjectStatusChangeRequest(string? Reason);

public sealed record DeleteProjectRequest(string? Reason, string? ConfirmProjectTitle);

public sealed record ProjectListResponse(
    IReadOnlyList<ProjectListItemResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);

public class ProjectListItemResponse
{
    public Guid ProjectId { get; init; }
    public string CustomerName { get; init; } = "";
    public string Item { get; init; } = "";
    public string ProjectCode { get; init; } = "";
    public string ProjectTitle { get; init; } = "";
    public int ActivePanelCount { get; init; }
    public DateOnly DeliveryDate { get; init; }
    public Guid SalesOwnerUserId { get; init; }
    public string SalesOwnerName { get; init; } = "";
    public string? PackagingMethod { get; init; }
    public string? DeliveryLocation { get; init; }
    public string Status { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? SalesAmount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrencyCode { get; init; }
}

public sealed class ProjectDetailResponse : ProjectListItemResponse
{
    public string? StatusReason { get; init; }
}

public class DeletedProjectListItemResponse : ProjectListItemResponse
{
    public DateTimeOffset DeletedAtUtc { get; init; }
    public Guid? DeletedByUserId { get; init; }
    public string? DeletedByUserName { get; init; }
    public string DeleteReason { get; init; } = "";
}

public sealed record DeletedProjectListResponse(
    IReadOnlyList<DeletedProjectListItemResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed class DeletedProjectDetailResponse : DeletedProjectListItemResponse
{
    public string? StatusReason { get; init; }
    public IReadOnlyList<PanelPlaceholderResponse> Panels { get; init; } = [];
    public IReadOnlyList<ProjectAuditEventResponse> AuditHistory { get; init; } = [];
}

public sealed record PanelPlaceholderResponse(
    Guid PanelId,
    Guid ProjectId,
    int SequenceNumber,
    string DisplayCode,
    string? PanelName,
    decimal? Width,
    decimal? Height,
    decimal? Depth,
    string PanelStatus,
    bool PanelInfoCompleted,
    bool QrEligible,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProjectAuditHistoryResponse(IReadOnlyList<ProjectAuditEventResponse> Items);

public sealed class ProjectAuditEventResponse
{
    public Guid AuditEventId { get; init; }
    public string EntityType { get; init; } = "";
    public Guid EntityId { get; init; }
    public Guid ProjectId { get; init; }
    public string Action { get; init; } = "";

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
}

public sealed record SalesOwnerResponse(Guid UserId, string DisplayName);
