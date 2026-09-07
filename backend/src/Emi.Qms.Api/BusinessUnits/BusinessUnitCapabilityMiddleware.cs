using Emi.Qms.Api.Authorization;

namespace Emi.Qms.Api.BusinessUnits;

public sealed record BusinessUnitAccessDeniedResponse(string ErrorCode, string Message);

public sealed class BusinessUnitCapabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, DatabaseConnectionStringProvider connectionStringProvider)
    {
        if (!connectionStringProvider.BusinessUnits.Enabled
            || context.User.Identity?.IsAuthenticated != true
            || !context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var selection = BusinessUnitRequestContextFeature.Get(context);
        if (selection is null)
        {
            await DenyAsync(context, "business_unit_context_missing");
            return;
        }

        var claimAccessStatus = context.User.FindFirst(
            QmsClaimTypes.BusinessUnitAccessStatus)?.Value;
        var hasSelectedLocalProfile = selection.IsSelected
            && string.Equals(
                claimAccessStatus,
                BusinessUnitAccessStatuses.Selected,
                StringComparison.Ordinal);
        if (!hasSelectedLocalProfile)
        {
            if (context.Request.Path.Equals("/api/me", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/api/admin/user-access")
                || context.Request.Path.StartsWithSegments("/api/admin/business-unit-access")
                || (selection.IsOverallAdministrator
                    && context.Request.Path.Equals("/api/runtime-mode", StringComparison.OrdinalIgnoreCase)))
            {
                await next(context);
                return;
            }

            await DenyAsync(
                context,
                string.Equals(
                    claimAccessStatus,
                    BusinessUnitAccessStatuses.LocalProfilePending,
                    StringComparison.Ordinal)
                    ? "business_unit_local_profile_pending"
                    : selection.Reason);
            return;
        }

        if (string.Equals(selection.Target!.Code, BusinessUnitCodes.Osan, StringComparison.Ordinal)
            && !IsOsanAllowedRequest(context.Request))
        {
            await DenyAsync(context, "business_unit_capability_disabled");
            return;
        }

        await next(context);
    }

    private static bool IsOsanAllowedRequest(HttpRequest request)
    {
        var path = request.Path;
        if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsPost(request.Method))
            && (path.Equals("/api/osan/projects", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/osan/projects/", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (HttpMethods.IsGet(request.Method)
            && path.StartsWithSegments("/api/osan/projects", out var osanProjectRemaining)
            && Guid.TryParse(osanProjectRemaining.Value?.Trim('/'), out _))
        {
            return true;
        }

        if (path.Equals("/api/me", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/me/profile-photo")
            || path.Equals("/api/runtime-mode", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/business-units")
            || path.StartsWithSegments("/api/admin/user-access")
            || path.StartsWithSegments("/api/admin/business-unit-access"))
        {
            return true;
        }

        if (HttpMethods.IsGet(request.Method)
            && path.Equals("/api/admin/users", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!HttpMethods.IsPatch(request.Method)
            || !path.StartsWithSegments("/api/admin/users", out var remaining))
        {
            return false;
        }

        return Guid.TryParse(remaining.Value?.Trim('/'), out _);
    }

    private static Task DenyAsync(HttpContext context, string errorCode)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new BusinessUnitAccessDeniedResponse(
            errorCode,
            "선택한 사업부에서 사용할 수 없는 기능입니다."));
    }
}
