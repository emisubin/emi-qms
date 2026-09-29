namespace Emi.Qms.Api.BusinessUnits;

public sealed record BusinessUnitRoute(string? Code, bool IsCommon);

/// <summary>Resolve the business from a fixed URL before authentication and endpoint matching.</summary>
public sealed class BusinessUnitRouteMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, DatabaseConnectionStringProvider connections)
    {
        var path = context.Request.Path;
        string? code = null;
        PathString remaining;
        var common = false;
        if (path.StartsWithSegments("/cheongju/api", out remaining)) code = BusinessUnitCodes.Cheongju;
        else if (path.StartsWithSegments("/osan/api", out remaining)) code = BusinessUnitCodes.Osan;
        else if (path.StartsWithSegments("/access/api", out remaining)) common = true;
        else
        {
            if (connections.BusinessUnits.Enabled && path.StartsWithSegments("/api"))
            {
                await DenyAsync(context, "business_unit_route_required");
                return;
            }
            await next(context);
            return;
        }

        var businessPath = new PathString("/api" + remaining.Value);
        var header = context.Request.Headers[BusinessUnitHeaderNames.Selection];
        if (code is not null && header.Count > 0
            && (header.Count != 1 || !string.Equals(header.ToString().Trim(), code, StringComparison.OrdinalIgnoreCase)))
        {
            await DenyAsync(context, "business_unit_route_mismatch");
            return;
        }
        if ((!connections.BusinessUnits.Enabled && code == BusinessUnitCodes.Osan)
            || (common && !IsCommonPath(businessPath))
            || (code == BusinessUnitCodes.Cheongju && businessPath.StartsWithSegments("/api/osan")))
        {
            await DenyAsync(context, "business_unit_capability_disabled");
            return;
        }
        context.Features.Set(new BusinessUnitRoute(code, common));
        context.Request.Path = businessPath;
        try { await next(context); }
        finally { context.Request.Path = path; }
    }

    internal static bool IsCommonPath(PathString path) =>
        path.Equals("/api/me", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/runtime-mode", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/business-units")
        || path.StartsWithSegments("/api/admin/user-access")
        || path.StartsWithSegments("/api/admin/business-unit-access");

    private static async Task DenyAsync(HttpContext context, string code)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new BusinessUnitAccessDeniedResponse(code,
            "요청한 사업부에서 사용할 수 없는 기능입니다."));
    }
}
