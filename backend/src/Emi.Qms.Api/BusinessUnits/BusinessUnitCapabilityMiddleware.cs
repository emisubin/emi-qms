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
        if (HttpMethods.IsGet(request.Method)
            && (path.Equals("/api/osan/dashboard", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/osan/dashboard/", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsPost(request.Method))
            && (path.Equals("/api/osan/projects", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/osan/projects/", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (path.StartsWithSegments("/api/osan/projects", out var osanProjectRemaining)
            && IsAllowedOsanProjectPath(request.Method, osanProjectRemaining.Value))
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

    private static bool IsAllowedOsanProjectPath(string method, string? remaining)
    {
        var segments = (remaining ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 2
            && string.Equals(segments[0], "import", StringComparison.OrdinalIgnoreCase))
        {
            return (HttpMethods.IsGet(method)
                    && string.Equals(segments[1], "template", StringComparison.OrdinalIgnoreCase))
                || (HttpMethods.IsPost(method)
                    && (string.Equals(segments[1], "preview", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(segments[1], "apply", StringComparison.OrdinalIgnoreCase)));
        }
        if (segments.Length == 0 || !Guid.TryParse(segments[0], out _))
        {
            return false;
        }

        if (HttpMethods.IsGet(method))
        {
            return segments.Length == 1
                || (segments.Length == 2 && string.Equals(segments[1], "management", StringComparison.OrdinalIgnoreCase))
                || (segments.Length == 2 && string.Equals(segments[1], "qr", StringComparison.OrdinalIgnoreCase))
                || (segments.Length == 3 && string.Equals(segments[1], "progress", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(segments[2], "photo-edits", StringComparison.OrdinalIgnoreCase))
                || (segments.Length == 2
                    && string.Equals(segments[1], "progress", StringComparison.OrdinalIgnoreCase))
                || (segments.Length == 4
                    && string.Equals(segments[1], "progress", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(segments[2], "photos", StringComparison.OrdinalIgnoreCase)
                    && Guid.TryParse(segments[3], out _));
        }

        if (segments.Length == 1 && (HttpMethods.IsPut(method) || HttpMethods.IsDelete(method))) return true;
        if (HttpMethods.IsPost(method) && segments.Length >= 3
            && string.Equals(segments[1], "progress", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "photo-edits", StringComparison.OrdinalIgnoreCase))
        {
            return segments.Length == 3 || (segments.Length == 5 && Guid.TryParse(segments[3], out _)
                && (string.Equals(segments[4], "approve", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segments[4], "save", StringComparison.OrdinalIgnoreCase)));
        }
        return HttpMethods.IsPost(method)
            && segments.Length == 3
            && string.Equals(segments[1], "progress", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(segments[2], "start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segments[2], "completions", StringComparison.OrdinalIgnoreCase));
    }

    private static Task DenyAsync(HttpContext context, string errorCode)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new BusinessUnitAccessDeniedResponse(
            errorCode,
            "선택한 사업부에서 사용할 수 없는 기능입니다."));
    }
}
