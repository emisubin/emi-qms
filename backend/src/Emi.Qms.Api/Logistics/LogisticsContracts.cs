namespace Emi.Qms.Api.Logistics;

public static class LogisticsStages
{
    public const string Packing = "packing";
    public const string Departure = "departure";
    public const string Delivery = "delivery";

    public static string? Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Packing => Packing,
        Departure => Departure,
        Delivery => Delivery,
        _ => null
    };

    public static string WorkStage(string stage) => stage switch
    {
        Packing => "PackingCompleted",
        Departure => "DepartureProcessed",
        Delivery => "DeliveryCompleted",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };
}

public sealed record LogisticsQueueResponse(
    string Stage,
    int TodayCount,
    int BlockedCount,
    IReadOnlyList<LogisticsProjectQueue> Projects,
    IReadOnlyList<LogisticsDraftSummary> Drafts);

public sealed record LogisticsDraftSummary(
    Guid TargetId,
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    string Stage,
    string DisplayCode,
    int Version,
    int EvidenceCount,
    DateTimeOffset CreatedAtUtc);

public sealed record LogisticsProjectQueue(
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    IReadOnlyList<LogisticsQueueItem> Items);

public sealed record LogisticsQueueItem(
    Guid TargetId,
    string TargetType,
    string DisplayCode,
    string Title,
    string SupportingText,
    IReadOnlyList<Guid> PanelIds,
    IReadOnlyList<string> PanelCodes,
    int Version,
    string Status,
    bool HasOpenPending,
    bool CanMutate);

public sealed record LogisticsEvidenceResponse(
    Guid EvidenceId,
    string OwnerType,
    string DisplayName,
    string NormalizedMime,
    int ByteSize,
    string? AltText,
    DateTimeOffset CreatedAtUtc);

public sealed record LogisticsDraftResponse(
    Guid TargetId,
    Guid ProjectId,
    string Stage,
    string DisplayCode,
    string Status,
    int Version,
    DateOnly? DepartureDate,
    IReadOnlyList<Guid> PanelIds,
    IReadOnlyList<Guid> UnitIds,
    IReadOnlyList<LogisticsEvidenceResponse> Evidence);

public sealed record LogisticsProjectHistoryResponse(
    Guid ProjectId,
    IReadOnlyList<LogisticsProjectHistoryItem> Items);

public sealed record LogisticsProjectHistoryItem(
    Guid TargetId,
    string Stage,
    string DisplayCode,
    string Status,
    int Version,
    string? Note,
    string? Specification,
    string? WeightText,
    DateOnly? DepartureDate,
    IReadOnlyList<string> PanelCodes,
    IReadOnlyList<string> UnitCodes,
    IReadOnlyList<LogisticsEvidenceResponse> Evidence,
    string CreatedByName,
    DateTimeOffset CreatedAtUtc,
    string? FinalizedByName,
    DateTimeOffset? FinalizedAtUtc,
    string? CancelledByName,
    DateTimeOffset? CancelledAtUtc);

public sealed record LogisticsMutationResponse(
    Guid OperationId,
    Guid ProjectId,
    Guid TargetId,
    string Stage,
    string Status,
    int Version,
    string NextStage,
    bool Replayed);

public sealed record CreatePackingUnitRequest(
    Guid OperationId,
    Guid ProjectId,
    IReadOnlyList<Guid>? PanelIds,
    string? Note,
    string? Specification,
    string? WeightText);

public sealed record ReplacePackingPanelsRequest(
    Guid OperationId,
    int? ExpectedVersion,
    IReadOnlyList<Guid>? PanelIds);

public sealed record CreateLogisticsBatchRequest(
    Guid OperationId,
    Guid ProjectId,
    IReadOnlyList<Guid>? UnitIds,
    DateOnly? DepartureDate);

public sealed record ReplaceLogisticsBatchUnitsRequest(
    Guid OperationId,
    int? ExpectedVersion,
    IReadOnlyList<Guid>? UnitIds,
    DateOnly? DepartureDate);

public sealed record FinalizeLogisticsRequest(Guid OperationId, int? ExpectedVersion);
public sealed record CancelLogisticsDraftRequest(Guid OperationId, int? ExpectedVersion);

public sealed record LogisticsEvidenceContent(byte[] Content, string NormalizedMime, string DisplayName);

public enum LogisticsMutationStatus { Success, NotFound, Forbidden, Validation, Conflict }

public sealed class LogisticsMutationResult<T>
{
    private LogisticsMutationResult(
        LogisticsMutationStatus status,
        T? value = default,
        string? message = null,
        Dictionary<string, string[]>? errors = null)
    {
        Status = status;
        Value = value;
        Message = message;
        Errors = errors ?? [];
    }

    public LogisticsMutationStatus Status { get; }
    public T? Value { get; }
    public string? Message { get; }
    public Dictionary<string, string[]> Errors { get; }

    public static LogisticsMutationResult<T> Success(T value) => new(LogisticsMutationStatus.Success, value);
    public static LogisticsMutationResult<T> NotFound() => new(LogisticsMutationStatus.NotFound);
    public static LogisticsMutationResult<T> Forbidden() => new(LogisticsMutationStatus.Forbidden);
    public static LogisticsMutationResult<T> Conflict(string message) => new(LogisticsMutationStatus.Conflict, message: message);
    public static LogisticsMutationResult<T> Validation(string field, string message)
        => new(LogisticsMutationStatus.Validation, errors: new Dictionary<string, string[]> { [field] = [message] });
}
