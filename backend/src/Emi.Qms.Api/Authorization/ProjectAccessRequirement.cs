using Microsoft.AspNetCore.Authorization;

namespace Emi.Qms.Api.Authorization;

public sealed class ProjectAccessRequirement : IAuthorizationRequirement;

public sealed class ProjectAccessAuthorizationHandler(IAuthorizationAuditLogger auditLogger)
    : AuthorizationHandler<ProjectAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.Resource is not HttpContext httpContext)
        {
            return auditLogger.LogDeniedAsync(
                context.User,
                null,
                "missing_http_context",
                null,
                CancellationToken.None);
        }

        var projectKey = Convert.ToString(httpContext.Request.RouteValues["projectId"], System.Globalization.CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            return auditLogger.LogDeniedAsync(
                context.User,
                httpContext,
                "missing_project_route_value",
                null,
                CancellationToken.None);
        }

        if (context.User.HasClaim(QmsClaimTypes.Permission, Identity.QmsPermissions.ProjectAccessAll)
            || context.User.HasClaim(QmsClaimTypes.Project, projectKey))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return auditLogger.LogDeniedAsync(
            context.User,
            httpContext,
            "project_access_denied",
            projectKey,
            CancellationToken.None);
    }
}
