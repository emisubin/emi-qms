namespace Emi.Qms.Api.Sales;

public sealed record SalesBillingPeriodResponse(DateOnly PeriodStart, DateOnly PeriodEnd, bool IsRecommended);

public sealed record SalesBillingCandidateResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    string CustomerName,
    string Item,
    string? DeliveryLocation,
    DateOnly FirstDepartureDate,
    DateOnly LastDepartureDate,
    int ActivePanelCount,
    int DepartedPanelCount,
    int OpenPendingCount,
    decimal? SalesAmount,
    string CurrencyCode,
    string SalesOwnerName,
    bool Requested,
    Guid? RequestBatchId,
    long? RequestNumber,
    DateTimeOffset? RequestedAtUtc,
    bool CanSelect,
    string? BlockedReason);

public sealed record SalesBillingCandidateListResponse(
    SalesBillingPeriodResponse Period,
    int CandidateCount,
    int SelectableCount,
    int RequestedCount,
    IReadOnlyList<SalesBillingCandidateResponse> Items);

public sealed record CreateSalesBillingRequest(
    Guid OperationId,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    IReadOnlyList<Guid>? ProjectIds,
    string? Note);

public sealed record SalesBillingBatchResponse(
    Guid BatchId,
    long RequestNumber,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int ProjectCount,
    string FileName,
    string Sha256,
    string? Note,
    string CreatedByName,
    DateTimeOffset CreatedAtUtc,
    bool Replayed);

public sealed record SalesBillingBatchListResponse(IReadOnlyList<SalesBillingBatchResponse> Items);

public sealed record SalesBillingFileResponse(
    Guid BatchId,
    string FileName,
    string ContentType,
    byte[] Content,
    string Sha256);

public sealed record SalesBillingProjectStatusResponse(
    bool Requested,
    Guid? BatchId,
    long? RequestNumber,
    DateTimeOffset? RequestedAtUtc,
    bool AccountingIssueConfirmed);
