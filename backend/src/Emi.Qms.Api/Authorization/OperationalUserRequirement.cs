using Microsoft.AspNetCore.Authorization;

namespace Emi.Qms.Api.Authorization;

public sealed class OperationalUserRequirement : IAuthorizationRequirement;

public sealed class OperationalUserAuthorizationHandler : AuthorizationHandler<OperationalUserRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationalUserRequirement requirement)
    {
        var isApprovalPending = string.Equals(
            context.User.FindFirst(QmsClaimTypes.ApprovalPending)?.Value,
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);
        var isInactive = string.Equals(
            context.User.FindFirst(QmsClaimTypes.Inactive)?.Value,
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

        if (!isApprovalPending && !isInactive)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
