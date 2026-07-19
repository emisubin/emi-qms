using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;

namespace Emi.Qms.Api.PendingTypes;

public static class PendingTypeEndpointExtensions
{
    public static IEndpointRouteBuilder MapPendingTypeEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/pending-types").RequireAuthorization();

        api.MapGet("", async (PendingTypeStore store, ClaimsPrincipal user, CancellationToken token) =>
            HasPermission(user, QmsPermissions.PendingTypeManage)
                ? Results.Ok(await store.GetCatalogAsync(token))
                : Results.Forbid())
            .WithName("GetPendingTypeCatalog");

        api.MapGet("/manual-options", async (PendingTypeStore store, ClaimsPrincipal user, CancellationToken token) =>
            HasPermission(user, QmsPermissions.PendingRead)
                ? Results.Ok(await store.GetManualOptionsAsync(token))
                : Results.Forbid())
            .WithName("GetPendingTypeManualOptions");

        api.MapGet("/filter-options", async (PendingTypeStore store, ClaimsPrincipal user, CancellationToken token) =>
            HasPermission(user, QmsPermissions.PendingRead)
                ? Results.Ok(await store.GetFilterOptionsAsync(token))
                : Results.Forbid())
            .WithName("GetPendingTypeFilterOptions");

        api.MapPost("", async (CreatePendingTypeRequest request, PendingTypeStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = AuthorizeManager(user);
            return actor.Error ?? ToResult(await store.CreateAsync(request, actor.UserId!.Value, token), Results.Ok);
        }).WithName("CreatePendingType");

        api.MapPut("/{code}", async (string code, UpdatePendingTypeRequest request, PendingTypeStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = AuthorizeManager(user);
            return actor.Error ?? ToResult(await store.UpdateAsync(code, request, actor.UserId!.Value, token), Results.Ok);
        }).WithName("UpdatePendingType");

        api.MapPost("/{code}/activate", async (string code, SetPendingTypeActiveRequest request, PendingTypeStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = AuthorizeManager(user);
            return actor.Error ?? ToResult(await store.SetActiveAsync(code, request, true, actor.UserId!.Value, token), Results.Ok);
        }).WithName("ActivatePendingType");

        api.MapPost("/{code}/deactivate", async (string code, SetPendingTypeActiveRequest request, PendingTypeStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = AuthorizeManager(user);
            return actor.Error ?? ToResult(await store.SetActiveAsync(code, request, false, actor.UserId!.Value, token), Results.Ok);
        }).WithName("DeactivatePendingType");

        api.MapPut("/reorder", async (ReorderPendingTypesRequest request, PendingTypeStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = AuthorizeManager(user);
            return actor.Error ?? ToResult(await store.ReorderAsync(request, actor.UserId!.Value, token), Results.Ok);
        }).WithName("ReorderPendingTypes");

        return app;
    }

    private static (Guid? UserId, IResult? Error) AuthorizeManager(ClaimsPrincipal user)
    {
        if (!HasPermission(user, QmsPermissions.PendingTypeManage))
        {
            return (null, Results.Forbid());
        }
        return Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var userId)
            ? (userId, null)
            : (null, Results.Unauthorized());
    }

    private static bool HasPermission(ClaimsPrincipal user, string permissionCode)
        => user.Identity?.IsAuthenticated == true && user.HasClaim(QmsClaimTypes.Permission, permissionCode);

    private static IResult ToResult<T>(PendingTypeMutationResult<T> result, Func<T, IResult> success)
        => result.Status switch
        {
            PendingTypeMutationStatus.Success when result.Value is not null => success(result.Value),
            PendingTypeMutationStatus.NotFound => Results.NotFound(),
            PendingTypeMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "Pending 유형이 변경되었습니다. 새로고침해 주세요.",
                statusCode: StatusCodes.Status409Conflict),
            PendingTypeMutationStatus.Validation when result.Errors is not null => Results.ValidationProblem(result.Errors),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
}
