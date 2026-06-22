using Microsoft.AspNetCore.Authorization;

namespace Emi.Qms.Api.Authorization;

public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

public sealed class PermissionAuthorizationHandler(IAuthorizationAuditLogger auditLogger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.User.HasClaim(QmsClaimTypes.Permission, requirement.PermissionCode))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return auditLogger.LogDeniedAsync(
            context.User,
            context.Resource as HttpContext,
            $"missing_permission:{requirement.PermissionCode}",
            null,
            CancellationToken.None);
    }
}
