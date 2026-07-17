using Emi.Qms.Api.Procurement;

namespace Emi.Qms.Api.Materials;

public static class MaterialReceiptStatuses
{
    public const string Arrived = "Arrived";
    public const string IqcRequested = "IqcRequested";
    public const string Passed = "Passed";
    public const string FailedBlocked = "FailedBlocked";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

public static class IqcDecisionModes
{
    public const string Legacy = "Legacy";
    public const string Detailed = "Detailed";
}

public sealed record MaterialReceiptSummaryResponse(
    int PendingArrivalCount,
    int WaitingIqcCount,
    int FailedBlockedCount,
    int ReadyToConfirmCount,
    int CompletedItemCount,
    int CustomerSuppliedItemCount,
    int CustomerSupplyOverdueCount);

public sealed record MaterialReceiptListResponse(
    MaterialReceiptSummaryResponse Summary,
    IReadOnlyList<MaterialReceivingItemResponse> Items);

public sealed class MaterialReceivingItemResponse
{
    public Guid ItemId { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectTitle { get; init; } = "";
    public string ProjectCode { get; init; } = "";
    public string? OrderItem { get; init; }
    public string? SupplierName { get; init; }
    public string SupplyType { get; init; } = ProcurementSupplyTypes.Purchased;
    public DateOnly? ExpectedReceiptDate { get; init; }
    public decimal? OrderQuantity { get; init; }
    public string? OrderUnit { get; init; }
    public bool ArrivalsClosed { get; init; }
    public DateTimeOffset? ArrivalsClosedAtUtc { get; init; }
    public bool ReceiptCompleted { get; init; }
    public decimal? ArrivedQuantity { get; init; }
    public decimal? ConfirmedQuantity { get; init; }
    public decimal? RemainingQuantity { get; init; }
    public decimal? ProcessingQuantity { get; init; }
    public bool CustomerSupplyOverdue { get; init; }
    public int RowVersion { get; init; }
    public IReadOnlyList<MaterialReceiptResponse> Receipts { get; init; } = [];
}

public sealed class MaterialReceiptResponse
{
    public Guid ReceiptId { get; init; }
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; }
    public DateOnly ArrivalDate { get; init; }
    public string? Note { get; init; }
    public string Status { get; init; } = "";
    public bool IsLegacy { get; init; }
    public int Version { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? ConfirmedAtUtc { get; init; }
    public string? CancellationReason { get; init; }
    public IReadOnlyList<MaterialIqcAttemptResponse> IqcAttempts { get; init; } = [];
}

public sealed record MaterialIqcAttemptResponse(
    Guid AttemptId,
    int AttemptNumber,
    string Status,
    string DecisionMode,
    string? Reason,
    Guid? PendingIssueId,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    Guid? ReportId,
    string? ReportStatus,
    string? PdfStatus);

public sealed record MaterialIqcQueueResponse(
    IReadOnlyList<MaterialIqcQueueItemResponse> Items);

public sealed record MaterialIqcQueueItemResponse(
    Guid AttemptId,
    Guid ReceiptId,
    Guid ProcurementItemId,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    string? OrderItem,
    decimal? Quantity,
    string? Unit,
    int AttemptNumber,
    int ReceiptVersion,
    string SupplyType,
    string Status,
    string DecisionMode,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    Guid? PendingIssueId,
    string? Reason,
    Guid? ReportId,
    string? ReportStatus,
    string? PdfStatus);

public sealed record RegisterMaterialArrivalRequest(
    decimal? Quantity,
    string? Unit,
    decimal? OrderQuantity,
    string? OrderUnit,
    DateOnly? ArrivalDate,
    string? Note);

public sealed record MaterialReceiptVersionRequest(int? ExpectedVersion);

public sealed record MaterialIqcResultRequest(
    int? ExpectedReceiptVersion,
    string? Result,
    string? Reason);

public sealed record CancelMaterialReceiptRequest(
    int? ExpectedVersion,
    string? Reason);

public sealed record CloseMaterialArrivalsRequest(
    int? ExpectedRowVersion,
    string? Reason);

public sealed record MaterialReceiptActionResponse(
    Guid ProcurementItemId,
    Guid? ReceiptId,
    Guid? IqcAttemptId,
    Guid? PendingIssueId,
    string Status,
    bool ReceiptCompleted);

public enum MaterialsMutationStatus
{
    Success,
    NotFound,
    Validation,
    Conflict
}

public sealed class MaterialsMutationResult<T>
{
    private MaterialsMutationResult(
        MaterialsMutationStatus status,
        T? value = default,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? message = null)
    {
        Status = status;
        Value = value;
        Errors = errors ?? new Dictionary<string, string[]>();
        Message = message;
    }

    public MaterialsMutationStatus Status { get; }
    public T? Value { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public string? Message { get; }

    public static MaterialsMutationResult<T> Success(T value) => new(MaterialsMutationStatus.Success, value);
    public static MaterialsMutationResult<T> NotFound() => new(MaterialsMutationStatus.NotFound);
    public static MaterialsMutationResult<T> Validation(IReadOnlyDictionary<string, string[]> errors) => new(MaterialsMutationStatus.Validation, errors: errors);
    public static MaterialsMutationResult<T> Conflict(string message) => new(MaterialsMutationStatus.Conflict, message: message);
}
