namespace Emi.Qms.Api.Sales;

public sealed record SalesKpiResponse(
    int Year,
    string Currency,
    string DefaultCurrency,
    IReadOnlyList<int> AvailableYears,
    IReadOnlyList<string> AvailableCurrencies,
    IReadOnlyList<SalesKpiMonthResponse> Months,
    SalesKpiSummaryResponse Kpi,
    SalesPipelineResponse Pipeline,
    int MissingAmountCount);

public sealed record SalesKpiMonthResponse(
    int Month,
    decimal RevenueAmount,
    decimal? TargetAmount,
    int SettlementCount);

public sealed record SalesKpiSummaryResponse(
    decimal CurrentMonthRevenue,
    decimal RevenueTotal,
    decimal? TargetTotal,
    int RegisteredTargetMonthCount,
    decimal? AchievementRate,
    decimal? RemainingTargetAmount,
    decimal? ExceededTargetAmount);

public sealed record SalesPipelineResponse(decimal Amount, int ProjectCount);

public sealed record SalesKpiMonthDetailResponse(
    int Year,
    int Month,
    string Currency,
    IReadOnlyList<SalesKpiProjectResponse> Projects);

public sealed record SalesKpiProjectResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    DateOnly InvoiceIssuedDate,
    decimal Amount);

public sealed record SalesTargetsResponse(
    int Year,
    string Currency,
    IReadOnlyList<SalesTargetMonthResponse> Months);

public sealed record SalesTargetMonthResponse(int Month, decimal? Amount, int? Version);

public sealed record SaveSalesTargetsRequest(
    int Year,
    string Currency,
    IReadOnlyList<SaveSalesTargetMonthRequest> Months);

public sealed record SaveSalesTargetMonthRequest(int Month, decimal Amount, int? ExpectedVersion);
