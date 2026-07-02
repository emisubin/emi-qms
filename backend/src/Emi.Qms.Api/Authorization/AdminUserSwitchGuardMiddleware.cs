namespace Emi.Qms.Api.Authorization;

public static class AdminUserSwitchDefaults
{
    public const string HeaderName = "X-Qms-Test-User";

    public const string ReasonDisabled = "disabled";
    public const string ReasonInvalidTestUser = "invalid_test_user";
    public const string ReasonNotAllowed = "not_allowed";

    public static readonly IReadOnlySet<string> AllowedDevelopmentUserKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "dev-admin",
            "dev-sales",
            "dev-production",
            "dev-procurement",
            "dev-materials",
            "dev-manufacturing",
            "dev-quality",
            "dev-logistics",
            "dev-viewer"
        };
}

public sealed class AdminUserSwitchGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey(AdminUserSwitchDefaults.HeaderName))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Microsoft 365 로그인이 필요합니다." });
            return;
        }

        var deniedReason = context.User.FindFirst(QmsClaimTypes.TestUserSwitchDeniedReason)?.Value;
        if (!string.IsNullOrWhiteSpace(deniedReason))
        {
            context.Response.StatusCode = string.Equals(deniedReason, AdminUserSwitchDefaults.ReasonInvalidTestUser, StringComparison.Ordinal)
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = MessageFor(deniedReason) });
            return;
        }

        if (!context.User.HasClaim(QmsClaimTypes.IsTestUserSwitch, bool.TrueString))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "검수 사용자 전환을 사용할 수 없습니다." });
            return;
        }

        await next(context);
    }

    private static string MessageFor(string deniedReason)
    {
        return deniedReason switch
        {
            AdminUserSwitchDefaults.ReasonDisabled => "검수 사용자 전환이 비활성화되어 있습니다.",
            AdminUserSwitchDefaults.ReasonInvalidTestUser => "허용되지 않은 검수 사용자입니다.",
            _ => "검수 사용자 전환 권한이 없습니다."
        };
    }
}
