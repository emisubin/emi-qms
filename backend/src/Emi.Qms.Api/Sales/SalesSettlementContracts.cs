namespace Emi.Qms.Api.Sales;

public sealed record SalesSettlementDetailResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    string ProjectStatus,
    string SettlementStatus,
    int Version,
    int ActivePanelCount,
    int DeliveredPanelCount,
    int OpenPendingCount,
    DateOnly? InvoiceIssuedDate,
    string? InvoiceNumber,
    string? Note,
    DateTimeOffset? CompletedAtUtc,
    string? CompletedByName,
    bool AllPanelsDelivered,
    bool NoOpenPending,
    bool InvoiceIssued,
    bool CanComplete,
    bool CanMutate,
    string PendingLink);

public sealed record SaveSalesSettlementDraftRequest(
    int? ExpectedVersion,
    DateOnly? InvoiceIssuedDate,
    string? InvoiceNumber,
    string? Note);

public sealed record CompleteSalesSettlementRequest(
    Guid OperationId,
    int? ExpectedVersion,
    DateOnly? InvoiceIssuedDate,
    string? InvoiceNumber,
    string? Note);

public sealed record SalesSettlementMutationResponse(
    Guid OperationId,
    Guid ProjectId,
    Guid SettlementId,
    string Status,
    int Version,
    bool Replayed);

public enum SalesSettlementMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Validation,
    Conflict
}

public sealed class SalesSettlementMutationResult<T>
{
    private SalesSettlementMutationResult(
        SalesSettlementMutationStatus status,
        T? value = default,
        string? message = null,
        Dictionary<string, string[]>? errors = null)
    {
        Status = status;
        Value = value;
        Message = message;
        Errors = errors ?? [];
    }

    public SalesSettlementMutationStatus Status { get; }
    public T? Value { get; }
    public string? Message { get; }
    public Dictionary<string, string[]> Errors { get; }

    public static SalesSettlementMutationResult<T> Success(T value) => new(SalesSettlementMutationStatus.Success, value);
    public static SalesSettlementMutationResult<T> NotFound() => new(SalesSettlementMutationStatus.NotFound);
    public static SalesSettlementMutationResult<T> Forbidden() => new(SalesSettlementMutationStatus.Forbidden);
    public static SalesSettlementMutationResult<T> Conflict(string message) => new(SalesSettlementMutationStatus.Conflict, message: message);
    public static SalesSettlementMutationResult<T> Validation(string field, string message)
        => new(SalesSettlementMutationStatus.Validation, errors: new Dictionary<string, string[]> { [field] = [message] });
}
