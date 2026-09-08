namespace Emi.Qms.Api.Identity;

public static class ApprovalReadinessPolicy
{
    public static bool IsReady(
        bool isActive,
        string? departmentCode,
        IReadOnlyCollection<string> roleCodes)
    {
        if (!isActive || string.IsNullOrWhiteSpace(departmentCode))
        {
            return false;
        }

        var defaultRoleCode = DepartmentIdentityPolicy.GetDefaultRoleCode(departmentCode);
        return defaultRoleCode is not null
            && roleCodes.Contains(defaultRoleCode, StringComparer.Ordinal);
    }

    public static bool IsApprovalPending(UserAuthorizationProfile profile)
    {
        return string.Equals(profile.User.AuthProvider, QmsAuthProviders.EntraId, StringComparison.Ordinal)
            && profile.User.IsActive
            && !IsReady(
                profile.User.IsActive,
                profile.Department?.Code,
                profile.Roles.Select(role => role.Code).ToArray());
    }

    public static bool IsApprovalPending(
        string authProvider,
        bool isActive,
        string? departmentCode,
        IReadOnlyCollection<string> roleCodes)
    {
        return string.Equals(authProvider, QmsAuthProviders.EntraId, StringComparison.Ordinal)
            && isActive
            && !IsReady(isActive, departmentCode, roleCodes);
    }
}

public static class RoleAssignmentSources
{
    public const string Explicit = "explicit";
    public const string DepartmentDefault = "department-default";
    public const string OverallAdministrator = "overall-administrator";
}
