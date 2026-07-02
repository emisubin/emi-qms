namespace Emi.Qms.Api.Identity;

public static class QmsAuthProviders
{
    public const string Dev = "Dev";
    public const string EntraId = "EntraId";
}

public sealed record Department(Guid Id, string Code, string Name);

public sealed record Role(Guid Id, string Code, string Name);

public sealed record Permission(Guid Id, string Code, string Name);

public sealed record QmsUser(
    Guid Id,
    string DevelopmentUserKey,
    string DisplayName,
    string? DepartmentCode,
    bool IsActive,
    string AuthProvider = QmsAuthProviders.Dev,
    string? Email = null);

public sealed record QmsProject(Guid Id, string ProjectKey, string ProjectNumber, string Name);

public sealed record UserProjectAccess(Guid UserId, Guid ProjectId);

public sealed record UserSummary(
    string DevelopmentUserKey,
    string DisplayName,
    string? DepartmentCode,
    IReadOnlyList<string> Roles,
    Guid? UserId = null,
    string? Email = null,
    string AuthProvider = QmsAuthProviders.Dev,
    bool IsActive = true,
    bool ApprovalPending = false,
    string? DepartmentId = null,
    string? DepartmentName = null,
    bool IsReadOnly = true);

public sealed record UserAuthorizationProfile(
    QmsUser User,
    Department? Department,
    IReadOnlyList<Role> Roles,
    IReadOnlyList<Permission> Permissions,
    IReadOnlyList<QmsProject> ProjectAccess)
{
    public bool HasPermission(string permissionCode)
    {
        return Permissions.Any(permission => string.Equals(permission.Code, permissionCode, StringComparison.Ordinal));
    }

    public bool CanAccessProject(string projectKey)
    {
        return HasPermission(QmsPermissions.ProjectReadAll)
            || ProjectAccess.Any(project => string.Equals(project.ProjectKey, projectKey, StringComparison.Ordinal));
    }
}
