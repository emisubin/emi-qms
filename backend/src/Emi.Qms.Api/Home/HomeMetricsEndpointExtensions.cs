using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.Home;

public static class HomeMetricsEndpointExtensions
{
    public static IEndpointRouteBuilder MapHomeMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/home/department-metrics", async (
            ClaimsPrincipal principal,
            IIdentityStore identityStore,
            HomeMetricsStore store,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();
            var profile = await identityStore.GetProfileByUserIdAsync(userId.Value, cancellationToken);
            if (profile is null || !profile.User.IsActive) return Results.Unauthorized();
            var approvalPending = profile.User.AuthProvider == QmsAuthProviders.EntraId && profile.Roles.Count == 0;
            if (approvalPending)
            {
                return Results.Ok(new HomeMetricsResponse(profile.Department?.Code, profile.Department?.Name, []));
            }

            var permissions = principal.FindAll(QmsClaimTypes.Permission)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal);
            var isSystemAdministrator = profile.Roles.Any(role =>
                string.Equals(role.Code, QmsRoles.SystemAdministrator, StringComparison.Ordinal));
            return Results.Ok(await store.GetAsync(
                profile.Department?.Code,
                profile.Department?.Name,
                userId.Value,
                permissions,
                isSystemAdministrator,
                ProjectEndpointExtensions.GetProjectAccessScope(principal),
                cancellationToken));
        })
        .RequireAuthorization("AuthenticatedIdentity")
        .WithName("GetHomeDepartmentMetrics");

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
