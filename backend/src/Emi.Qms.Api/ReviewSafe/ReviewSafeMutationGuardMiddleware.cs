namespace Emi.Qms.Api.ReviewSafe;

public sealed class ReviewSafeMutationGuardMiddleware(
    RequestDelegate next,
    IConfiguration configuration)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ReviewSafeMode.IsEnabled(configuration))
        {
            await next(context);
            return;
        }

        var overrideMethod = context.Request.Headers["X-HTTP-Method-Override"].ToString();
        if (!SafeMethods.Contains(context.Request.Method)
            || (!string.IsNullOrWhiteSpace(overrideMethod) && !SafeMethods.Contains(overrideMethod)))
        {
            context.Response.StatusCode = StatusCodes.Status423Locked;
            await context.Response.WriteAsJsonAsync(new ReviewSafeLockedResponse(
                ReviewSafeMode.ErrorCode,
                ReviewSafeMode.LockedMessage));
            return;
        }

        await next(context);
    }
}

public sealed record ReviewSafeLockedResponse(string ErrorCode, string Message);
