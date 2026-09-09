namespace Emi.Qms.Api.OsanProjects;

public static class OsanDashboardStatuses
{
    public const string All = "All";
    public const string NotStarted = "NotStarted";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";

    public static bool IsValid(string value) =>
        value is All or NotStarted or InProgress or Completed;
}

public static class OsanDashboardViews
{
    public const string Progress = "progress";
    public const string Home = "home";

    public static bool IsValid(string value) => value is Progress or Home;
}

public sealed record OsanDashboardQuery(
    string Search,
    string Status,
    int Page,
    int PageSize,
    string View = OsanDashboardViews.Progress);

public sealed record OsanDashboardResponse(
    OsanDashboardSummaryResponse Summary,
    IReadOnlyList<OsanDashboardProjectResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);

public sealed record OsanDashboardSummaryResponse(
    long TotalCount,
    long NotStartedCount,
    long InProgressCount,
    long CompletedCount);

public sealed record OsanDashboardProjectResponse(
    Guid ProjectId,
    string Title,
    string ProjectCode,
    string CustomerName,
    string ProductName,
    string? PoNumber,
    string? WorkOrderNumber,
    int Quantity,
    DateOnly DeliveryDate,
    string Status,
    int CompletedStepCount,
    int TotalStepCount,
    int ProgressPercent,
    IReadOnlyList<OsanDashboardStageResponse> Stages);

public sealed record OsanDashboardStageResponse(
    int SequenceNumber,
    string StepCode,
    string StepName,
    int CompletedTargetCount,
    int TotalTargetCount);
