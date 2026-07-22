using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.Ul891Sets;

public static class Ul891SetEndpointExtensions
{
    public static IEndpointRouteBuilder MapUl891SetEndpoints(this IEndpointRouteBuilder app)
    {
        var sets = app.MapGroup("/api/projects/{projectId:guid}");

        sets.MapGet("/set-structure", async (Guid projectId, Ul891SetStore store, ClaimsPrincipal user, CancellationToken token) =>
            ToResult(await store.GetStructureAsync(
                projectId,
                Has(user, QmsPermissions.ProjectUpdate),
                Has(user, QmsPermissions.PanelInfoUpdate),
                token)))
            .RequireAuthorization(ReadPolicy())
            .WithName("GetUl891SetStructure");

        sets.MapPost("/set-specs", async (
            Guid projectId, AddUl891SetSpecRequest request,
            Ul891SetStore store, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() :
                ToResult(await store.AddSpecAsync(projectId, request, actor.Value, context.TraceIdentifier, token));
        }).RequireAuthorization(QmsPolicies.ProjectUpdate).WithName("AddUl891SetSpec");

        sets.MapPut("/set-specs/{specId:guid}/versions/{versionId:guid}", async (
            Guid projectId, Guid specId, Guid versionId, UpdateUl891DraftRequest request,
            Ul891SetStore store, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() :
                ToResult(await store.UpdateDraftAsync(projectId, specId, versionId, request, actor.Value, context.TraceIdentifier, token));
        }).RequireAuthorization(QmsPolicies.PanelInfoUpdate).WithName("UpdateUl891SetDraft");

        sets.MapPost("/set-specs/{specId:guid}/versions/{versionId:guid}/publish", async (
            Guid projectId, Guid specId, Guid versionId, PublishUl891VersionRequest request,
            Ul891SetStore store, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() :
                ToResult(await store.PublishAsync(projectId, specId, versionId, request, actor.Value, context.TraceIdentifier, token));
        }).RequireAuthorization(QmsPolicies.PanelInfoUpdate).WithName("PublishUl891SetVersion");

        sets.MapPost("/set-specs/{specId:guid}/versions", async (
            Guid projectId, Guid specId, CreateUl891VersionRequest request,
            Ul891SetStore store, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() :
                ToResult(await store.CreateVersionAsync(projectId, specId, request, actor.Value, context.TraceIdentifier, token));
        }).RequireAuthorization(QmsPolicies.PanelInfoUpdate).WithName("CreateUl891SetVersion");

        sets.MapPost("/set-specs/{specId:guid}/apply-version", async (
            Guid projectId, Guid specId, ApplyUl891VersionRequest request,
            Ul891SetStore store, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() :
                ToResult(await store.ApplyVersionAsync(projectId, specId, request, actor.Value, context.TraceIdentifier, token));
        }).RequireAuthorization(QmsPolicies.PanelInfoUpdate).WithName("ApplyUl891SetVersion");

        sets.MapPost("/set-specs/{specId:guid}/instances/increase", async (
            Guid projectId, Guid specId, IncreaseUl891InstancesRequest request,
            Ul891SetStore store, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() :
                ToResult(await store.IncreaseAsync(projectId, specId, request, actor.Value, context.TraceIdentifier, token));
        }).RequireAuthorization(QmsPolicies.ProjectUpdate).WithName("IncreaseUl891SetInstances");

        sets.MapPost("/set-instances/cancel", async (
            Guid projectId, CancelUl891InstancesRequest request,
            Ul891SetStore store, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() :
                ToResult(await store.CancelInstancesAsync(projectId, request, actor.Value, context.TraceIdentifier, token));
        }).RequireAuthorization(QmsPolicies.ProjectUpdate).WithName("CancelUl891SetInstances");

        sets.MapPost("/recovery-cases/{caseId:guid}/recover", async (
            Guid projectId, Guid caseId, RecoverUl891CaseRequest request,
            Ul891SetStore store, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() :
                ToResult(await store.RecoverCaseAsync(projectId, caseId, request, actor.Value, context.TraceIdentifier, token));
        }).RequireAuthorization(QmsPolicies.SalesSettle).WithName("RecoverUl891CancellationCharge");

        sets.MapGet("/monthly-billing", async (
            Guid projectId, MonthlyBillingStore store, ClaimsPrincipal user, CancellationToken token) =>
            ToResult(await store.GetAsync(projectId, Has(user, QmsPermissions.ProjectSalesAmountRead), Has(user, QmsPermissions.SalesSettle), token)))
            .RequireAuthorization(ReadPolicy()).WithName("GetUl891MonthlyBilling");

        sets.MapPost("/monthly-billing/open", async (
            Guid projectId, OpenMonthlyBillingLedgerRequest request, MonthlyBillingStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() : ToResult(await store.OpenAsync(projectId, request, actor.Value, token));
        }).RequireAuthorization(QmsPolicies.SalesSettle).WithName("OpenUl891MonthlyBillingLedger");

        sets.MapPost("/monthly-billing/{ledgerId:guid}/revisions", async (
            Guid projectId, Guid ledgerId, CreateMonthlyBillingRevisionRequest request, MonthlyBillingStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() : ToResult(await store.CreateRevisionAsync(projectId, ledgerId, request, actor.Value, token));
        }).RequireAuthorization(QmsPolicies.SalesSettle).WithName("CreateUl891MonthlyBillingRevision");

        sets.MapPost("/monthly-billing/{ledgerId:guid}/confirm", async (
            Guid projectId, Guid ledgerId, ConfirmMonthlyBillingRequest request, MonthlyBillingStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = UserId(user); return actor is null ? Results.Unauthorized() : ToResult(await store.ConfirmAsync(projectId, ledgerId, request, actor.Value, token));
        }).RequireAuthorization(QmsPolicies.SalesSettle).WithName("ConfirmUl891MonthlyBilling");

        return app;
    }

    private static IResult ToResult<T>(ProjectMutationResult<T> result) => result.Status switch
    {
        ProjectMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
        ProjectMutationStatus.NotFound => Results.NotFound(),
        ProjectMutationStatus.ValidationFailed => Results.ValidationProblem(result.Errors ?? new Dictionary<string, string[]>()),
        ProjectMutationStatus.Conflict => Results.Problem(title: result.Message ?? "UL891 세트 작업을 수행할 수 없습니다.", statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

    private static Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder> ReadPolicy() => policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead));
    private static bool Has(ClaimsPrincipal user, string permission) => user.HasClaim(QmsClaimTypes.Permission, permission);
    private static Guid? UserId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var value) ? value : null;
}
