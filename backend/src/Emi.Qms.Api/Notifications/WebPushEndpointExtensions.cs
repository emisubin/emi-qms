using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public static class WebPushEndpointExtensions
{
    public static IEndpointRouteBuilder MapWebPushEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/my/web-push", async (
            ClaimsPrincipal principal,
            WebPushSubscriptionStore store,
            IOptionsMonitor<NotificationOptions> options,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(principal);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await store.GetConfigurationAsync(userId.Value, options.CurrentValue.WebPush, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("GetMyWebPushConfiguration");

        app.MapPost("/api/my/web-push/current-status", async (
            WebPushSubscriptionEndpointRequest request,
            ClaimsPrincipal principal,
            WebPushSubscriptionStore store,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(principal, userId => store.GetCurrentStatusAsync(userId, request.Endpoint, cancellationToken)))
        .RequireAuthorization()
        .WithName("GetMyCurrentWebPushStatus");

        app.MapPut("/api/my/web-push/subscriptions", async (
            WebPushSubscriptionRequest request,
            ClaimsPrincipal principal,
            WebPushSubscriptionStore store,
            IOptionsMonitor<NotificationOptions> options,
            CancellationToken cancellationToken) =>
        {
            var webPush = options.CurrentValue.WebPush;
            if (!webPush.Enabled || string.IsNullOrWhiteSpace(webPush.PublicKey))
            {
                return Results.Json(
                    new { code = "WebPushNotConfigured", message = "PWA 푸시 알림은 현재 준비 중입니다." },
                    statusCode: StatusCodes.Status409Conflict);
            }

            return await ExecuteAsync(
                principal,
                userId => store.UpsertAsync(userId, request, webPush, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("SaveMyWebPushSubscription");

        app.MapPost("/api/my/web-push/subscriptions/deactivate-current", async (
            WebPushDeactivateCurrentRequest request,
            ClaimsPrincipal principal,
            WebPushSubscriptionStore store,
            CancellationToken cancellationToken) =>
        {
            var reason = string.Equals(request.Reason, "Logout", StringComparison.Ordinal)
                ? "Logout"
                : "UserRequest";
            return await ExecuteAsync(
                principal,
                userId => store.DeactivateCurrentAsync(userId, request.Endpoint, reason, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("DeactivateMyCurrentWebPushSubscription");

        app.MapPost("/api/my/web-push/subscriptions/deactivate-all", async (
            ClaimsPrincipal principal,
            WebPushSubscriptionStore store,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(principal, userId => store.DeactivateAllAsync(userId, cancellationToken)))
        .RequireAuthorization()
        .WithName("DeactivateAllMyWebPushSubscriptions");

        return app;
    }

    private static async Task<IResult> ExecuteAsync<T>(ClaimsPrincipal principal, Func<Guid, Task<T>> action)
    {
        var userId = GetCurrentUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            return Results.Ok(await action(userId.Value));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { code = "InvalidWebPushSubscription", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Json(
                new { code = "WebPushSubscriptionUnavailable", message = exception.Message },
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
