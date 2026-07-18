namespace Emi.Qms.Api.Home;

public sealed record HomeMetricsResponse(
    string? DepartmentCode,
    string? DepartmentName,
    IReadOnlyList<HomeMetricResponse> Metrics);

public sealed record HomeMetricResponse(
    string Id,
    string Label,
    int Count,
    string Tone,
    string DestinationKey,
    string ActionLabel);
