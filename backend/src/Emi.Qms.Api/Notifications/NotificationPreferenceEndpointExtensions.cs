using System.Security.Claims;
using Emi.Qms.Api.Authorization;

namespace Emi.Qms.Api.Notifications;

public static class NotificationPreferenceEndpointExtensions
{
    public static IEndpointRouteBuilder MapNotificationPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/my/notification-preferences", async (
            ClaimsPrincipal principal,
            NotificationPreferenceStore store,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(principal);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.GetAsync(userId.Value, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("GetMyNotificationPreferences");

        app.MapPut("/api/my/notification-preferences", async (
            UpdateNotificationPreferencesRequest request,
            ClaimsPrincipal principal,
            NotificationPreferenceStore store,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(principal);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.SaveAsync(userId.Value, userId.Value, request, false, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("SaveMyNotificationPreferences");

        app.MapPost("/api/my/notification-preferences/reset", async (
            ResetNotificationPreferencesRequest request,
            ClaimsPrincipal principal,
            NotificationPreferenceStore store,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(principal);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.ResetAsync(userId.Value, userId.Value, request.ExpectedVersion, false, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("ResetMyNotificationPreferences");

        app.MapGet("/api/admin/users/{userId:guid}/notification-preferences", async (
            Guid userId,
            NotificationPreferenceStore store,
            CancellationToken cancellationToken) =>
            ToResult(await store.GetAsync(userId, cancellationToken)))
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("GetAdminUserNotificationPreferences");

        app.MapPut("/api/admin/users/{userId:guid}/notification-preferences", async (
            Guid userId,
            UpdateNotificationPreferencesRequest request,
            ClaimsPrincipal principal,
            NotificationPreferenceStore store,
            CancellationToken cancellationToken) =>
        {
            var actorUserId = GetCurrentUserId(principal);
            return actorUserId is null
                ? Results.Unauthorized()
                : ToResult(await store.SaveAsync(actorUserId.Value, userId, request, true, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("SaveAdminUserNotificationPreferences");

        app.MapPost("/api/admin/users/{userId:guid}/notification-preferences/reset", async (
            Guid userId,
            ResetNotificationPreferencesRequest request,
            ClaimsPrincipal principal,
            NotificationPreferenceStore store,
            CancellationToken cancellationToken) =>
        {
            var actorUserId = GetCurrentUserId(principal);
            return actorUserId is null
                ? Results.Unauthorized()
                : ToResult(await store.ResetAsync(actorUserId.Value, userId, request.ExpectedVersion, true, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ResetAdminUserNotificationPreferences");

        return app;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static IResult ToResult(NotificationPreferenceResult result)
    {
        if (result.Status == NotificationPreferenceResultStatus.Success && result.Response is not null)
        {
            return Results.Ok(result.Response);
        }

        var body = new
        {
            code = result.ErrorCode,
            message = result.ErrorMessage,
            errors = result.Errors
        };
        return result.Status switch
        {
            NotificationPreferenceResultStatus.NotFound => Results.Json(body, statusCode: StatusCodes.Status404NotFound),
            NotificationPreferenceResultStatus.Inactive => Results.Json(body, statusCode: StatusCodes.Status409Conflict),
            NotificationPreferenceResultStatus.Conflict => Results.Json(body, statusCode: StatusCodes.Status409Conflict),
            _ => Results.Json(body, statusCode: StatusCodes.Status400BadRequest)
        };
    }
}
