namespace Emi.Qms.Api.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; "
        + "base-uri 'self'; "
        + "object-src 'none'; "
        + "script-src 'self'; "
        + "style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data: blob:; "
        + "font-src 'self' data:; "
        + "connect-src 'self' https://login.microsoftonline.com https://*.microsoftonline.com https://graph.microsoft.com; "
        + "frame-src https://login.microsoftonline.com https://*.microsoftonline.com; "
        + "frame-ancestors 'self' https://teams.microsoft.com https://*.teams.microsoft.com; "
        + "form-action 'self' https://login.microsoftonline.com; "
        + "upgrade-insecure-requests";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=(), usb=(), serial=(), bluetooth=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
            headers["Cross-Origin-Resource-Policy"] = "same-site";
            headers.CacheControl = "private, no-store, max-age=0";
            headers.Pragma = "no-cache";
            headers.Expires = "0";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
