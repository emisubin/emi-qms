namespace Emi.Qms.Api.Identity;

public sealed record Department(Guid Id, string Code, string Name);

public sealed record Role(Guid Id, string Code, string Name);

public sealed record Permission(Guid Id, string Code, string Name);

public sealed record QmsUser(
    Guid Id,
    string DevelopmentUserKey,
    string DisplayName,
    string DepartmentCode,
    bool IsActive);

public sealed record QmsProject(Guid Id, string ProjectKey, string ProjectNumber, string Name);

public sealed record UserProjectAccess(Guid UserId, Guid ProjectId);

public sealed record UserSummary(
    string DevelopmentUserKey,
    string DisplayName,
    string DepartmentCode,
    IReadOnlyList<string> Roles);

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
        return HasPermission(QmsPermissions.ProjectAccessAll)
            || ProjectAccess.Any(project => string.Equals(project.ProjectKey, projectKey, StringComparison.Ordinal));
    }
}
