namespace Emi.Qms.Api.BusinessUnits;

public static class BusinessUnitCodes
{
    public const string Cheongju = "CHEONGJU";
    public const string Osan = "OSAN";

    public static bool IsKnown(string? value) =>
        string.Equals(value, Cheongju, StringComparison.Ordinal)
        || string.Equals(value, Osan, StringComparison.Ordinal);
}

public static class BusinessUnitHeaderNames
{
    public const string Selection = "X-Qms-Business-Unit";
}

public static class BusinessUnitAccessStatuses
{
    public const string Selected = "selected";
    public const string NoMembership = "no_membership";
    public const string SelectionRequired = "selection_required";
    public const string SelectionDenied = "selection_denied";
    public const string LocalProfilePending = "local_profile_pending";
}

public sealed record BusinessUnitRequestContext(
    string Status,
    Guid? DirectoryUserId,
    BusinessUnitDatabaseTarget? Target,
    IReadOnlyList<string> AllowedBusinessUnits,
    bool IsOverallAdministrator,
    string Reason)
{
    public bool IsSelected =>
        string.Equals(Status, BusinessUnitAccessStatuses.Selected, StringComparison.Ordinal)
        && Target is not null;
}

public static class BusinessUnitRequestContextFeature
{
    public static BusinessUnitRequestContext? Get(HttpContext? context) =>
        context?.Features.Get<BusinessUnitRequestContext>();

    public static void Set(HttpContext context, BusinessUnitRequestContext value)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(value);

        var current = Get(context);
        if (current is not null && current != value)
        {
            throw new InvalidOperationException("The business-unit request context is immutable once resolved.");
        }

        context.Features.Set(value);
    }
}

public sealed class BusinessUnitContextUnavailableException(string reason)
    : InvalidOperationException("A trusted business-unit context is required for database access.")
{
    public string Reason { get; } = reason;
}
