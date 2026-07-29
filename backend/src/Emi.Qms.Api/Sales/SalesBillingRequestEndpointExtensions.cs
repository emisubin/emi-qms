using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.Sales;

public static class SalesBillingRequestEndpointExtensions
{
    public static IEndpointRouteBuilder MapSalesBillingRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var billing = app.MapGroup("/api/sales/billing-requests");

        billing.MapGet("/candidates", async (
            DateOnly? periodStart,
            DateOnly? periodEnd,
            SalesBillingRequestStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
            ToResult(await store.ListCandidatesAsync(
                periodStart,
                periodEnd,
                Scope(user),
                HasPermission(user, QmsPermissions.ProjectSalesAmountRead),
                token)))
            .RequireAuthorization(QmsPolicies.SalesSettle)
            .WithName("ListSalesBillingRequestCandidates");

        billing.MapGet("", async (
            SalesBillingRequestStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
            ToResult(await store.ListBatchesAsync(Scope(user), token)))
            .RequireAuthorization(QmsPolicies.SalesSettle)
            .WithName("ListSalesBillingRequestBatches");

        billing.MapPost("", async (
            CreateSalesBillingRequest request,
            SalesBillingRequestStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var actorId = UserId(user);
            return actorId is null
                ? Results.Unauthorized()
                : ToResult(await store.CreateAsync(request, actorId.Value, Scope(user), token));
        })
            .RequireAuthorization(policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.SalesSettle));
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectSalesAmountRead));
            })
            .WithName("CreateSalesBillingRequestBatch");

        billing.MapGet("/{batchId:guid}/file", async (
            Guid batchId,
            SalesBillingRequestStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var actorId = UserId(user);
            if (actorId is null) return Results.Unauthorized();
            var result = await store.DownloadAsync(batchId, actorId.Value, Scope(user), token);
            return result.Status switch
            {
                SalesSettlementMutationStatus.Success when result.Value is not null => Results.File(
                    result.Value.Content,
                    result.Value.ContentType,
                    result.Value.FileName),
                SalesSettlementMutationStatus.NotFound => Results.NotFound(),
                SalesSettlementMutationStatus.Forbidden => Results.Forbid(),
                SalesSettlementMutationStatus.Validation => Results.ValidationProblem(result.Errors),
                SalesSettlementMutationStatus.Conflict => Results.Problem(title: result.Message, statusCode: StatusCodes.Status409Conflict),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
            };
        })
            .RequireAuthorization(policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.SalesSettle));
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectSalesAmountRead));
            })
            .WithName("DownloadSalesBillingRequestWorkbook");

        return app;
    }

    private static IResult ToResult<T>(SalesSettlementMutationResult<T> result) => result.Status switch
    {
        SalesSettlementMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
        SalesSettlementMutationStatus.NotFound => Results.NotFound(),
        SalesSettlementMutationStatus.Forbidden => Results.Forbid(),
        SalesSettlementMutationStatus.Validation => Results.ValidationProblem(result.Errors),
        SalesSettlementMutationStatus.Conflict => Results.Problem(title: result.Message, statusCode: StatusCodes.Status409Conflict),
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
