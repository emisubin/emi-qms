namespace Emi.Qms.Api.DeploymentMaintenance;

public sealed record DeploymentMaintenanceLockedResponse(string ErrorCode,string Message);

public sealed class DeploymentMaintenanceMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods=new(StringComparer.OrdinalIgnoreCase)
        {HttpMethods.Get,HttpMethods.Head,HttpMethods.Options};

    public async Task InvokeAsync(HttpContext context,DatabaseConnectionStringProvider provider)
    {
        var path=context.Request.Path;
        if(!path.StartsWithSegments("/api") || path.StartsWithSegments("/api/maintenance")
            || context.User.Identity?.IsAuthenticated!=true)
        {
            await next(context);return;
        }
        var overrideMethod=context.Request.Headers["X-HTTP-Method-Override"].ToString();
        if(SafeMethods.Contains(context.Request.Method)
            && (string.IsNullOrWhiteSpace(overrideMethod)||SafeMethods.Contains(overrideMethod)))
        {
            await next(context);return;
        }
        if(provider.GetCurrentBusinessUnit() is null)
        {
            await next(context);return;
        }
        await using var lease=await DeploymentMaintenanceLease.AcquireCurrentAsync(provider,context.RequestAborted);
        if(lease is null)
        {
            context.Response.StatusCode=StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new DeploymentMaintenanceLockedResponse(
                "release_maintenance","업데이트 중 저장할 수 없습니다. 완료 후 다시 시도해 주세요."));
            return;
        }
        await next(context);
    }
}
