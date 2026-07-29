using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.Logistics;

public static class LogisticsEndpointExtensions
{
    public static IEndpointRouteBuilder MapLogisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var logistics = app.MapGroup("/api/logistics");

        logistics.MapGet("/queue", async (string? stage, Guid? projectId, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
            Results.Ok(await store.ListAsync(stage, projectId, Scope(user), UserId(user), HasPermission(user, QmsPermissions.LogisticsShip), token)))
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
            .WithName("ListLogisticsQueue");

        logistics.MapGet("/projects/{projectId:guid}/history", async (
            Guid projectId, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var result = await store.GetProjectHistoryAsync(projectId, Scope(user), token);
            return result.Status == LogisticsMutationStatus.Success && result.Value is not null
                ? Results.Ok(result.Value)
                : Results.NotFound();
        })
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
            .WithName("GetLogisticsProjectHistory");

        logistics.MapGet("/{stage:regex(^(packing|departure|delivery)$)}/{targetId:guid}", async (
            string stage, Guid targetId, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var result = await store.GetDraftAsync(targetId, stage, Scope(user), token);
            return result.Status == LogisticsMutationStatus.Success && result.Value is not null
                ? Results.Ok(result.Value)
                : Results.NotFound();
        })
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
            .WithName("GetLogisticsDraft");

        logistics.MapPost("/packing-units", async (CreatePackingUnitRequest request, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
            await Mutate(user, (actor, scope) => store.CreatePackingUnitAsync(request, actor, scope, token)))
            .RequireAuthorization(QmsPolicies.LogisticsShip).WithName("CreateLogisticsPackingUnit");

        logistics.MapPut("/packing-units/{unitId:guid}/panels", async (Guid unitId, ReplacePackingPanelsRequest request, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
            await Mutate(user, (actor, scope) => store.ReplacePackingPanelsAsync(unitId, request, actor, scope, token)))
            .RequireAuthorization(QmsPolicies.LogisticsShip).WithName("ReplaceLogisticsPackingPanels");

        logistics.MapPost("/{stage:regex(^(departure|delivery)$)}-batches", async (string stage, CreateLogisticsBatchRequest request, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
            await Mutate(user, (actor, scope) => store.CreateBatchAsync(stage, request, actor, scope, token)))
            .RequireAuthorization(QmsPolicies.LogisticsShip).WithName("CreateLogisticsBatch");

        logistics.MapPut("/{stage:regex(^(departure|delivery)$)}-batches/{batchId:guid}/units", async (string stage, Guid batchId, ReplaceLogisticsBatchUnitsRequest request, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
            await Mutate(user, (actor, scope) => store.ReplaceBatchUnitsAsync(batchId, stage, request, actor, scope, token)))
            .RequireAuthorization(QmsPolicies.LogisticsShip).WithName("ReplaceLogisticsBatchUnits");

        logistics.MapPost("/{stage:regex(^(packing|departure|delivery)$)}/{targetId:guid}/evidence", async (
            string stage, Guid targetId, [FromForm] Guid operationId, [FromForm] int expectedVersion,
            [FromForm] string? altText, [FromForm] IFormFile file, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            if (file.Length is < 1 or > 10 * 1024 * 1024)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["파일 크기와 형식을 확인해 주세요."] });
            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, token);
            return await Mutate(user, (actor, scope) => store.AddEvidenceAsync(targetId, stage, operationId, expectedVersion, altText, memory.ToArray(), actor, scope, token));
        })
            .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024))
            .DisableAntiforgery()
            .RequireAuthorization(QmsPolicies.LogisticsShip).WithName("UploadLogisticsEvidence");

        logistics.MapDelete("/{stage:regex(^(packing|departure|delivery)$)}/{targetId:guid}/evidence/{evidenceId:guid}", async (
            string stage, Guid targetId, Guid evidenceId, Guid operationId, int? expectedVersion,
            LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
            await Mutate(user, (actor, scope) => store.RemoveEvidenceAsync(targetId, evidenceId, stage, operationId, expectedVersion, actor, scope, token)))
            .RequireAuthorization(QmsPolicies.LogisticsShip).WithName("DeleteLogisticsEvidence");

        logistics.MapPost("/{stage:regex(^(packing|departure|delivery)$)}/{targetId:guid}/finalize", async (
            string stage, Guid targetId, FinalizeLogisticsRequest request, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
            await Mutate(user, (actor, scope) => store.FinalizeAsync(targetId, stage, request, actor, scope, token)))
            .RequireAuthorization(QmsPolicies.LogisticsShip).WithName("FinalizeLogisticsOperation");

        logistics.MapPost("/{stage:regex(^(packing|departure|delivery)$)}/{targetId:guid}/cancel", async (
            string stage, Guid targetId, CancelLogisticsDraftRequest request, LogisticsStore store, ClaimsPrincipal user, CancellationToken token) =>
            await Mutate(user, (actor, scope) => store.CancelDraftAsync(targetId, stage, request, actor, scope, token)))
            .RequireAuthorization(QmsPolicies.LogisticsShip).WithName("CancelLogisticsDraft");

        logistics.MapGet("/evidence/{evidenceId:guid}/content", async (Guid evidenceId, LogisticsStore store, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            var result = await store.GetEvidenceAsync(evidenceId, Scope(user), token);
            if (result.Status == LogisticsMutationStatus.NotFound || result.Value is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(result.Value.Content, result.Value.NormalizedMime, result.Value.DisplayName);
        })
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
            .WithName("DownloadLogisticsEvidence");

        return app;
    }

    private static async Task<IResult> Mutate(ClaimsPrincipal user, Func<Guid, ProjectAccessScope, Task<LogisticsMutationResult<LogisticsMutationResponse>>> action)
    {
        var actor = UserId(user);
        return actor is null ? Results.Unauthorized() : ToResult(await action(actor.Value, Scope(user)));
    }

    private static IResult ToResult(LogisticsMutationResult<LogisticsMutationResponse> result) => result.Status switch
    {
        LogisticsMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
        LogisticsMutationStatus.NotFound => Results.NotFound(),
        LogisticsMutationStatus.Forbidden => Results.Forbid(),
        LogisticsMutationStatus.Validation => Results.ValidationProblem(result.Errors),
        LogisticsMutationStatus.Conflict => Results.Problem(title: result.Message ?? "물류 작업을 수행할 수 없습니다.", statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

    private static Guid? UserId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var value) ? value : null;
    private static bool HasPermission(ClaimsPrincipal user, string permission) => user.HasClaim(QmsClaimTypes.Permission, permission);
    private static ProjectAccessScope Scope(ClaimsPrincipal user) => new(
        user.HasClaim(QmsClaimTypes.Permission, QmsPermissions.ProjectReadAll),
        user.FindAll(QmsClaimTypes.Project).Select(claim => claim.Value).ToList());
}
