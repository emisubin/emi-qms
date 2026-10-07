using System.Security.Claims;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.Identity;

public sealed record OsanUserProjectCreatePermissionsRequest(
    IReadOnlyList<OsanUserProjectCreatePermissionUpdate?>? Items);

public static class OsanUserProjectCreatePermissionEndpointExtensions
{
    public static IEndpointRouteBuilder MapOsanUserProjectCreatePermissionEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/osan/admin/user-project-create-permissions").RequireAuthorization();

        api.MapGet("", async (
            OsanUserProjectCreatePermissionStore store,
            OsanDatabase database,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var denied = Guard(database, user);
            return denied ?? Results.Ok(new { items = await store.ListAsync(cancellationToken) });
        }).WithName("ListOsanUserProjectCreatePermissions");

        api.MapPut("", async (
            OsanUserProjectCreatePermissionsRequest request,
            OsanUserProjectCreatePermissionStore store,
            OsanDatabase database,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var denied = Guard(database, user);
            if (denied is not null) return denied;
            var actorUserId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (actorUserId is null) return Results.Unauthorized();
            var result = await store.SetAsync(request.Items, actorUserId.Value, cancellationToken);
            return result.Status switch
            {
                200 => Results.Ok(new { items = result.Items }),
                _ => Results.Json(new OsanProjectErrorResponse(
                    result.Code ?? "osan_user_permission_rejected",
                    result.Message ?? "사용자 권한을 저장할 수 없습니다."), statusCode: result.Status)
            };
        }).WithName("SetOsanUserProjectCreatePermissions");

        return app;
    }

    private static IResult? Guard(OsanDatabase database, ClaimsPrincipal user)
    {
        if (!string.Equals(
            database.GetCurrentBusinessUnit().Code,
            BusinessUnitCodes.Osan,
            StringComparison.Ordinal))
        {
            return Results.Json(new OsanProjectErrorResponse(
                "business_unit_capability_disabled",
                "오산 사업부를 선택해 주세요."), statusCode: StatusCodes.Status403Forbidden);
        }
        if (ProjectEndpointExtensions.GetCurrentUserId(user) is null) return Results.Unauthorized();
        return user.IsInRole(QmsRoles.SystemAdministrator) ? null : Results.Forbid();
    }
}
