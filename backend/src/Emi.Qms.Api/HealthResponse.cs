namespace Emi.Qms.Api;

public sealed record HealthResponse(string Name, string Status, DateTimeOffset CheckedAtUtc);

public sealed record ReadyHealthResponse(
    string Name,
    string Status,
    DatabaseHealthResult Database,
    DateTimeOffset CheckedAtUtc);

public sealed record DatabaseHealthResult(bool IsReady, string Reason);

public sealed record ReviewSafeReadyHealthResponse(
    string Name,
    string Status,
    DatabaseHealthResult Database,
    ReviewSafe.ReviewSafeRuntimeStatus ReviewSafe,
    DateTimeOffset CheckedAtUtc);
