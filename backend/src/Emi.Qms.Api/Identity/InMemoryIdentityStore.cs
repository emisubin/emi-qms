namespace Emi.Qms.Api.Identity;

public sealed class InMemoryIdentityStore : IIdentityStore
{
    public Task<UserAuthorizationProfile?> GetProfileByDevelopmentUserKeyAsync(
        string developmentUserKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = SeedIdentityData.Users.FirstOrDefault(candidate =>
            string.Equals(candidate.DevelopmentUserKey, developmentUserKey, StringComparison.Ordinal));

        return Task.FromResult(user is null ? null : BuildProfile(user));
    }

    public Task<UserAuthorizationProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = SeedIdentityData.Users.FirstOrDefault(candidate => candidate.Id == userId);
        return Task.FromResult(user is null ? null : BuildProfile(user));
    }

    public Task<QmsProject?> GetProjectByKeyAsync(string projectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var project = SeedIdentityData.Projects.FirstOrDefault(candidate =>
            string.Equals(candidate.ProjectKey, projectKey, StringComparison.Ordinal));

        return Task.FromResult(project);
    }

    public Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var users = SeedIdentityData.Users
            .OrderBy(user => user.DevelopmentUserKey, StringComparer.Ordinal)
            .Select(user =>
            {
                SeedIdentityData.UserRoles.TryGetValue(user.DevelopmentUserKey, out var roles);
                return new UserSummary(
                    user.DevelopmentUserKey,
                    user.DisplayName,
                    user.DepartmentCode,
                    roles ?? []);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<UserSummary>>(users);
    }

    private static UserAuthorizationProfile BuildProfile(QmsUser user)
    {
        var department = SeedIdentityData.Departments.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, user.DepartmentCode, StringComparison.Ordinal));

        SeedIdentityData.UserRoles.TryGetValue(user.DevelopmentUserKey, out var roleCodes);
        SeedIdentityData.UserProjectAccess.TryGetValue(user.DevelopmentUserKey, out var projectKeys);

        var roles = SeedIdentityData.Roles
            .Where(role => roleCodes?.Contains(role.Code, StringComparer.Ordinal) == true)
            .OrderBy(role => role.Code, StringComparer.Ordinal)
            .ToList();

        var permissionCodes = roles
            .SelectMany(role => SeedIdentityData.RolePermissions.TryGetValue(role.Code, out var permissions) ? permissions : [])
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var permissions = SeedIdentityData.Permissions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .OrderBy(permission => permission.Code, StringComparer.Ordinal)
            .ToList();

        var projects = SeedIdentityData.Projects
            .Where(project => projectKeys?.Contains(project.ProjectKey, StringComparer.Ordinal) == true)
            .OrderBy(project => project.ProjectKey, StringComparer.Ordinal)
            .ToList();

        return new UserAuthorizationProfile(user, department, roles, permissions, projects);
    }
}
