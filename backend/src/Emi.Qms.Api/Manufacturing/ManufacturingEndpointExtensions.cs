using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.Manufacturing;

public static class ManufacturingEndpointExtensions
{
    public static IEndpointRouteBuilder MapManufacturingEndpoints(this IEndpointRouteBuilder app)
    {
        var manufacturing = app.MapGroup("/api/manufacturing");

        manufacturing.MapGet("/queue", async (
            Guid? projectId,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListAsync(
                GetProjectAccessScope(user),
                GetCurrentUserId(user),
                HasPermission(user, QmsPermissions.ManufacturingWorkTimeRead),
                HasPermission(user, QmsPermissions.ManufacturingUpdate),
                projectId,
                cancellationToken)))
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("ListManufacturingQueue");

        manufacturing.MapGet("/release-candidates", async (
            Guid? projectId,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListReleaseCandidatesAsync(
                GetProjectAccessScope(user),
                projectId,
                cancellationToken)))
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("ListManufacturingReleaseCandidates");

        manufacturing.MapPost("/releases", async (
            ReleaseManufacturingRequest request,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var actorId = GetCurrentUserId(user);
            return actorId is null
                ? Results.Unauthorized()
                : ToReleaseResult(await store.ReleaseAsync(
                    request,
                    actorId.Value,
                    GetProjectAccessScope(user),
                    cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.ProductionPlanUpdate)
        .WithName("ReleaseManufacturingPanels");

        manufacturing.MapGet("/panels/{panelId:guid}", async (
            Guid panelId,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var detail = await store.GetDetailAsync(
                panelId,
                GetProjectAccessScope(user),
                GetCurrentUserId(user),
                HasPermission(user, QmsPermissions.ManufacturingWorkTimeRead),
                HasPermission(user, QmsPermissions.ManufacturingUpdate),
                cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        })
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("GetManufacturingPanel");

        manufacturing.MapGet("/action-departments", async (
            ManufacturingStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListActionDepartmentsAsync(cancellationToken)))
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithName("ListManufacturingActionDepartments");

        manufacturing.MapPost("/executions/start", async (
            StartManufacturingRequest request,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await Mutate(user, (actorId, scope) => store.StartAsync(request, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithName("StartManufacturingExecution");

        manufacturing.MapPost("/executions/{executionId:guid}/check-step", async (
            Guid executionId,
            CheckManufacturingStepRequest request,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await Mutate(user, (actorId, scope) => store.CheckStepAsync(executionId, request, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithName("CheckManufacturingStep");

        manufacturing.MapPost("/executions/assembly-batch", async (
            AssemblyBatchManufacturingRequest request,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var actorId = GetCurrentUserId(user);
            return actorId is null
                ? Results.Unauthorized()
                : ToAssemblyBatchResult(await store.CompleteAssemblyBatchAsync(
                    request,
                    actorId.Value,
                    GetProjectAccessScope(user),
                    cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithName("CompleteManufacturingAssemblyBatch");

        manufacturing.MapPost("/executions/{executionId:guid}/stop", async (
            Guid executionId,
            StopManufacturingRequest request,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await Mutate(user, (actorId, scope) => store.StopAsync(executionId, request, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithName("StopManufacturingExecution");

        manufacturing.MapPost("/executions/{executionId:guid}/resume", async (
            Guid executionId,
            ResumeManufacturingRequest request,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await Mutate(user, (actorId, scope) => store.ResumeAsync(executionId, request, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithName("ResumeManufacturingExecution");

        manufacturing.MapPost("/executions/{executionId:guid}/complete", async (
            Guid executionId,
            CompleteManufacturingRequest request,
            ManufacturingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await Mutate(user, (actorId, scope) => store.CompleteAsync(executionId, request, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithName("CompleteManufacturingExecution");

        return app;
    }

    private static async Task<IResult> Mutate(
        ClaimsPrincipal user,
        Func<Guid, ProjectAccessScope, Task<ManufacturingMutationResult<ManufacturingMutationResponse>>> mutation)
    {
        var actorId = GetCurrentUserId(user);
        return actorId is null ? Results.Unauthorized() : ToResult(await mutation(actorId.Value, GetProjectAccessScope(user)));
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

    private static bool HasPermission(ClaimsPrincipal user, string permission)
        => user.HasClaim(QmsClaimTypes.Permission, permission);

    private static IResult ToResult(ManufacturingMutationResult<ManufacturingMutationResponse> result)
    {
        return result.Status switch
        {
            ManufacturingMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
            ManufacturingMutationStatus.NotFound => Results.NotFound(),
            ManufacturingMutationStatus.Validation => Results.ValidationProblem(result.Errors),
            ManufacturingMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 제조 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static IResult ToReleaseResult(ManufacturingMutationResult<ManufacturingReleaseResponse> result)
    {
        return result.Status switch
        {
            ManufacturingMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
            ManufacturingMutationStatus.NotFound => Results.NotFound(),
            ManufacturingMutationStatus.Validation => Results.ValidationProblem(result.Errors),
            ManufacturingMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 제조 투입을 처리할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static IResult ToAssemblyBatchResult(
        ManufacturingMutationResult<AssemblyBatchManufacturingResponse> result)
    {
        return result.Status switch
        {
            ManufacturingMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
            ManufacturingMutationStatus.NotFound => Results.NotFound(),
            ManufacturingMutationStatus.Validation => Results.ValidationProblem(result.Errors),
            ManufacturingMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "선택 패널의 조립 단계를 완료할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
