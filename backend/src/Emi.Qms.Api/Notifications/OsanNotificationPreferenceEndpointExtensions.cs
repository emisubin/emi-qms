using System.Security.Claims;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;

namespace Emi.Qms.Api.Notifications;

public static class OsanNotificationPreferenceEndpointExtensions
{
    public static IEndpointRouteBuilder MapOsanNotificationPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/osan/my/notification-preferences", async (
            ClaimsPrincipal principal,
            DatabaseConnectionStringProvider connectionStrings,
            OsanNotificationPreferenceStore store,
            CancellationToken cancellationToken) =>
        {
            var userId = CurrentUserId(principal);
            return userId is null ? Results.Unauthorized()
                : !IsOsan(connectionStrings) || !principal.IsInRole(QmsRoles.SystemAdministrator) ? Results.Forbid()
                : ToResult(await store.GetAsync(userId.Value, cancellationToken));
        }).RequireAuthorization().WithName("GetMyOsanNotificationPreferences");

        app.MapPut("/api/osan/my/notification-preferences", async (
            UpdateOsanNotificationPreferencesRequest request,
            ClaimsPrincipal principal,
            DatabaseConnectionStringProvider connectionStrings,
            OsanNotificationPreferenceStore store,
            CancellationToken cancellationToken) =>
        {
            var userId = CurrentUserId(principal);
            return userId is null ? Results.Unauthorized()
                : !IsOsan(connectionStrings) || !principal.IsInRole(QmsRoles.SystemAdministrator) ? Results.Forbid()
                : ToResult(await store.SaveAsync(userId.Value, request, cancellationToken));
        }).RequireAuthorization().WithName("SaveMyOsanNotificationPreferences");

        return app;
    }

    private static bool IsOsan(DatabaseConnectionStringProvider connectionStrings) =>
        string.Equals(connectionStrings.GetCurrentBusinessUnit()?.Code, BusinessUnitCodes.Osan, StringComparison.Ordinal);

    private static Guid? CurrentUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(QmsClaimTypes.UserId)?.Value, out var userId) ? userId : null;

    private static IResult ToResult(OsanNotificationPreferenceResult result) => result.Status switch
    {
        OsanNotificationPreferenceResultStatus.Success => Results.Ok(result.Response),
        OsanNotificationPreferenceResultStatus.Conflict => Results.Conflict(new { code = result.ErrorCode, message = result.ErrorMessage }),
        _ => Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage })
    };
}
