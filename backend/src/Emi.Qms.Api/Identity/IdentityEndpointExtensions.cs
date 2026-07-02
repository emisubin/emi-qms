using System.Security.Claims;
using Emi.Qms.Api.Authorization;

namespace Emi.Qms.Api.Identity;

public static class IdentityEndpointExtensions
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/me", async (
            ClaimsPrincipal principal,
            IIdentityStore identityStore,
            IConfiguration configuration,
            IHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var effectiveProfile = await GetProfileByClaimAsync(principal, identityStore, QmsClaimTypes.UserId, cancellationToken);
            if (effectiveProfile is null)
            {
                return Results.Unauthorized();
            }

            var actualProfile = await GetProfileByClaimAsync(principal, identityStore, QmsClaimTypes.ActualUserId, cancellationToken)
                ?? effectiveProfile;
            var adminUserSwitchEnabled = DevelopmentFeaturePolicy
                .EvaluateAdminUserSwitch(environment, configuration)
                .IsEnabled;

            return effectiveProfile is null
                ? Results.Unauthorized()
                : Results.Ok(CurrentUserResponse.From(effectiveProfile, actualProfile, principal, adminUserSwitchEnabled));
        })
        .RequireAuthorization("AuthenticatedIdentity")
        .WithName("GetCurrentUser");

        api.MapGet("/projects/{projectId}/overview", async (
            string projectId,
            IIdentityStore identityStore,
            CancellationToken cancellationToken) =>
        {
            var project = await identityStore.GetProjectByKeyAsync(projectId, cancellationToken);
            return project is null
                ? Results.NotFound()
                : Results.Ok(ProjectOverviewResponse.From(project));
        })
        .RequireAuthorization(QmsPolicies.ProjectRead)
        .WithName("GetProjectOverview");

        api.MapGet("/admin/users", async (
            IUserAdministrationStore userAdministrationStore,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await userAdministrationStore.GetSnapshotAsync(cancellationToken);
            return Results.Ok(AdminUsersResponse.From(snapshot));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("GetAdminUsers");

        api.MapPatch("/admin/users/{userId:guid}", async (
            Guid userId,
            UpdateUserAdministrationRequest request,
            ClaimsPrincipal principal,
            IUserAdministrationStore userAdministrationStore,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(principal);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await userAdministrationStore.UpdateEntraUserAsync(
                userId,
                request,
                currentUserId.Value,
                cancellationToken);

            return result.Succeeded && result.Snapshot is not null
                ? Results.Ok(AdminUsersResponse.From(result.Snapshot))
                : Results.BadRequest(new { message = result.ErrorMessage ?? "사용자 정보를 저장할 수 없습니다." });
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("UpdateAdminUser");

        return app;
    }

    private static async Task<UserAuthorizationProfile?> GetProfileByClaimAsync(
        ClaimsPrincipal principal,
        IIdentityStore identityStore,
        string claimType,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirst(claimType)?.Value;

        return Guid.TryParse(userIdValue, out var userId)
            ? await identityStore.GetProfileByUserIdAsync(userId, cancellationToken)
            : null;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}

public sealed record CurrentUserResponse(
    Guid UserId,
    string DevelopmentUserKey,
    string DisplayName,
    string? Email,
    string AuthProvider,
    bool IsActive,
    bool ApprovalPending,
    string? Department,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<ProjectAccessResponse> ProjectAccess,
    bool IsTestUserSwitch,
    string? TestUserKey,
    bool CanUseAdminTestUserSwitch,
    CurrentUserPrincipalResponse ActualUser,
    CurrentUserPrincipalResponse EffectiveUser)
{
    public static CurrentUserResponse From(
        UserAuthorizationProfile effectiveProfile,
        UserAuthorizationProfile actualProfile,
        ClaimsPrincipal principal,
        bool adminUserSwitchEnabled)
    {
        var effectiveApprovalPending = IsApprovalPending(effectiveProfile);
        var actualApprovalPending = IsApprovalPending(actualProfile);
        var canUseAdminTestUserSwitch = adminUserSwitchEnabled
            && actualProfile.User.IsActive
            && !actualApprovalPending
            && actualProfile.Roles.Any(role => string.Equals(role.Code, QmsRoles.SystemAdministrator, StringComparison.Ordinal));

        return new CurrentUserResponse(
            effectiveProfile.User.Id,
            effectiveProfile.User.DevelopmentUserKey,
            effectiveProfile.User.DisplayName,
            effectiveProfile.User.Email,
            effectiveProfile.User.AuthProvider,
            effectiveProfile.User.IsActive,
            effectiveApprovalPending,
            effectiveProfile.Department?.Code,
            effectiveProfile.Roles.Select(role => role.Code).OrderBy(code => code, StringComparer.Ordinal).ToList(),
            effectiveProfile.User.IsActive && !effectiveApprovalPending
                ? effectiveProfile.Permissions.Select(permission => permission.Code).OrderBy(code => code, StringComparer.Ordinal).ToList()
                : [],
            effectiveProfile.ProjectAccess.Select(ProjectAccessResponse.From).ToList(),
            principal.HasClaim(QmsClaimTypes.IsTestUserSwitch, bool.TrueString),
            principal.FindFirst(QmsClaimTypes.TestUserKey)?.Value,
            canUseAdminTestUserSwitch,
            CurrentUserPrincipalResponse.From(actualProfile, actualApprovalPending),
            CurrentUserPrincipalResponse.From(effectiveProfile, effectiveApprovalPending));
    }

    private static bool IsApprovalPending(UserAuthorizationProfile profile)
    {
        return profile.User.AuthProvider == QmsAuthProviders.EntraId
            && profile.User.IsActive
            && profile.Roles.Count == 0;
    }
}

public sealed record CurrentUserPrincipalResponse(
    Guid UserId,
    string DevelopmentUserKey,
    string DisplayName,
    string? Email,
    string AuthProvider,
    bool IsActive,
    bool ApprovalPending,
    string? Department,
    IReadOnlyList<string> Roles)
{
    public static CurrentUserPrincipalResponse From(UserAuthorizationProfile profile, bool approvalPending)
    {
        return new CurrentUserPrincipalResponse(
            profile.User.Id,
            profile.User.DevelopmentUserKey,
            profile.User.DisplayName,
            profile.User.Email,
            profile.User.AuthProvider,
            profile.User.IsActive,
            approvalPending,
            profile.Department?.Code,
            profile.Roles.Select(role => role.Code).OrderBy(code => code, StringComparer.Ordinal).ToList());
    }
}

public sealed record ProjectAccessResponse(string ProjectKey, string ProjectNumber, string Name)
{
    public static ProjectAccessResponse From(QmsProject project)
    {
        return new ProjectAccessResponse(project.ProjectKey, project.ProjectNumber, project.Name);
    }
}

public sealed record ProjectOverviewResponse(string ProjectKey, string ProjectNumber, string Name, string Status)
{
    public static ProjectOverviewResponse From(QmsProject project)
    {
        return new ProjectOverviewResponse(project.ProjectKey, project.ProjectNumber, project.Name, "authorization-foundation");
    }
}

public sealed record AdminUsersResponse(
    IReadOnlyList<AdminUserResponse> Users,
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminRoleResponse> Roles)
{
    public static AdminUsersResponse From(UserAdministrationSnapshot snapshot)
    {
        return new AdminUsersResponse(
            snapshot.Users.Select(AdminUserResponse.From).ToList(),
            snapshot.Departments.Select(AdminDepartmentResponse.From).ToList(),
            snapshot.Roles.Select(AdminRoleResponse.From).ToList());
    }
}

public sealed record AdminUserResponse(
    Guid UserId,
    string DevelopmentUserKey,
    string DisplayName,
    string? Email,
    string AuthProvider,
    bool IsActive,
    bool ApprovalPending,
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    IReadOnlyList<string> Roles,
    bool IsReadOnly)
{
    public static AdminUserResponse From(UserAdministrationUser user)
    {
        return new AdminUserResponse(
            user.UserId,
            user.DevelopmentUserKey,
            user.DisplayName,
            user.Email,
            user.AuthProvider,
            user.IsActive,
            user.ApprovalPending,
            user.DepartmentId,
            user.DepartmentCode,
            user.DepartmentName,
            user.Roles,
            user.IsReadOnly);
    }
}

public sealed record AdminDepartmentResponse(Guid DepartmentId, string Code, string Name)
{
    public static AdminDepartmentResponse From(Department department)
    {
        return new AdminDepartmentResponse(department.Id, department.Code, department.Name);
    }
}

public sealed record AdminRoleResponse(Guid RoleId, string Code, string Name)
{
    public static AdminRoleResponse From(Role role)
    {
        return new AdminRoleResponse(role.Id, role.Code, role.Name);
    }
}
