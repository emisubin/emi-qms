using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.Materials;

public static class PanelKittingEndpointExtensions
{
    public static IEndpointRouteBuilder MapPanelKittingEndpoints(this IEndpointRouteBuilder app)
    {
        var kitting = app.MapGroup("/api/materials/kitting");

        kitting.MapGet("", async (
            Guid? projectId,
            PanelKittingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListAsync(GetProjectAccessScope(user), projectId, cancellationToken)))
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("ListPanelKittingQueue");

        kitting.MapPost("/complete", async (
            CompletePanelKittingRequest request,
            PanelKittingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.CompleteAsync(
                    request,
                    userId.Value,
                    GetProjectAccessScope(user),
                    cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("CompletePanelKitting");

        return app;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static ProjectAccessScope GetProjectAccessScope(ClaimsPrincipal user)
    {
        return new ProjectAccessScope(
            user.HasClaim(QmsClaimTypes.Permission, QmsPermissions.ProjectReadAll),
            user.FindAll(QmsClaimTypes.Project).Select(claim => claim.Value).ToList());
    }

    private static IResult ToResult(MaterialsMutationResult<PanelKittingCompletionResponse> result)
    {
        return result.Status switch
        {
            MaterialsMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
            MaterialsMutationStatus.NotFound => Results.NotFound(),
            MaterialsMutationStatus.Validation => Results.ValidationProblem(result.Errors),
            MaterialsMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
