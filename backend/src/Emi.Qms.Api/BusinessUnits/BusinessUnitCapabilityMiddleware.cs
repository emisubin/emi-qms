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
                && !string.Equals(selection.Status, BusinessUnitAccessStatuses.SelectionDenied, StringComparison.Ordinal))
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
            && !IsOsanAllowedPath(context.Request.Path))
        {
            await DenyAsync(context, "business_unit_capability_disabled");
            return;
        }

        await next(context);
    }

    private static bool IsOsanAllowedPath(PathString path) =>
        path.Equals("/api/me", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/business-units");

    private static Task DenyAsync(HttpContext context, string errorCode)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new BusinessUnitAccessDeniedResponse(
            errorCode,
            "선택한 사업부에서 사용할 수 없는 기능입니다."));
    }
}
