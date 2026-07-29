using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.Sales;

public static class SalesSettlementEndpointExtensions
{
    public static IEndpointRouteBuilder MapSalesSettlementEndpoints(this IEndpointRouteBuilder app)
    {
        var settlements = app.MapGroup("/api/projects/{projectId:guid}/settlement");

        settlements.MapGet("", async (
            Guid projectId,
            SalesSettlementStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var result = await store.GetAsync(
                projectId,
                Scope(user),
                UserId(user),
                HasPermission(user, QmsPermissions.SalesSettle),
                token);
            return ToResult(result);
        })
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
            .WithName("GetSalesSettlement");

        settlements.MapPut("/draft", async (
            Guid projectId,
            SaveSalesSettlementDraftRequest request,
            SalesSettlementStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var actor = UserId(user);
            return actor is null
                ? Results.Unauthorized()
                : ToResult(await store.SaveDraftAsync(projectId, request, actor.Value, Scope(user), token));
        })
            .RequireAuthorization(QmsPolicies.SalesSettle)
            .WithName("SaveSalesSettlementDraft");

        settlements.MapPost("/complete", async (
            Guid projectId,
            CompleteSalesSettlementRequest request,
            SalesSettlementStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var actor = UserId(user);
            return actor is null
                ? Results.Unauthorized()
                : ToResult(await store.CompleteAsync(projectId, request, actor.Value, Scope(user), token));
        })
            .RequireAuthorization(QmsPolicies.SalesSettle)
            .WithName("CompleteSalesSettlement");

        return app;
    }

    private static IResult ToResult<T>(SalesSettlementMutationResult<T> result) => result.Status switch
    {
        SalesSettlementMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
        SalesSettlementMutationStatus.NotFound => Results.NotFound(),
        SalesSettlementMutationStatus.Forbidden => Results.Forbid(),
        SalesSettlementMutationStatus.Validation => Results.ValidationProblem(result.Errors),
        SalesSettlementMutationStatus.Conflict => Results.Problem(
            title: result.Message ?? "영업 정산 작업을 수행할 수 없습니다.",
            statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

    private static Guid? UserId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var value) ? value : null;

    private static bool HasPermission(ClaimsPrincipal user, string permission)
        => user.HasClaim(QmsClaimTypes.Permission, permission);

    private static ProjectAccessScope Scope(ClaimsPrincipal user) => new(
        user.HasClaim(QmsClaimTypes.Permission, QmsPermissions.ProjectReadAll),
        user.FindAll(QmsClaimTypes.Project).Select(claim => claim.Value).ToList());
}
