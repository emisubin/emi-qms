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
            CancellationToken cancellationToken) =>
        {
            var profile = await GetCurrentProfileAsync(principal, identityStore, cancellationToken);
            return profile is null
                ? Results.Unauthorized()
                : Results.Ok(CurrentUserResponse.From(profile));
        })
        .RequireAuthorization()
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
            IIdentityStore identityStore,
            CancellationToken cancellationToken) =>
        {
            var users = await identityStore.GetUsersAsync(cancellationToken);
            return Results.Ok(new AdminUsersResponse(users));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("GetAdminUsers");

        return app;
    }

    private static async Task<UserAuthorizationProfile?> GetCurrentProfileAsync(
        ClaimsPrincipal principal,
        IIdentityStore identityStore,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirst(QmsClaimTypes.UserId)?.Value;

        return Guid.TryParse(userIdValue, out var userId)
            ? await identityStore.GetProfileByUserIdAsync(userId, cancellationToken)
            : null;
    }
}

public sealed record CurrentUserResponse(
    string DevelopmentUserKey,
    string DisplayName,
    string? Department,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<ProjectAccessResponse> ProjectAccess)
{
    public static CurrentUserResponse From(UserAuthorizationProfile profile)
    {
        return new CurrentUserResponse(
            profile.User.DevelopmentUserKey,
            profile.User.DisplayName,
            profile.Department?.Code,
            profile.Roles.Select(role => role.Code).OrderBy(code => code, StringComparer.Ordinal).ToList(),
            profile.Permissions.Select(permission => permission.Code).OrderBy(code => code, StringComparer.Ordinal).ToList(),
            profile.ProjectAccess.Select(ProjectAccessResponse.From).ToList());
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

public sealed record AdminUsersResponse(IReadOnlyList<UserSummary> Users);
