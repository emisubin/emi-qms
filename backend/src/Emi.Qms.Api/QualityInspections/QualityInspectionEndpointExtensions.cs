using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.QualityInspections;

public static class QualityInspectionEndpointExtensions
{
    public static IEndpointRouteBuilder MapQualityInspectionEndpoints(this IEndpointRouteBuilder app)
    {
        var quality = app.MapGroup("/api/quality/inspections");

        quality.MapGet("/queue", async (
            string? stage,
            Guid? projectId,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListAsync(
                GetProjectAccessScope(user),
                GetCurrentUserId(user),
                HasPermission(user, QmsPermissions.QualityInspect),
                stage,
                projectId,
                cancellationToken)))
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("ListPanelQualityInspections");

        quality.MapGet("/panels/{panelId:guid}", async (
            Guid panelId,
            string stage,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var result = await store.GetDetailAsync(
                panelId,
                stage,
                GetProjectAccessScope(user),
                GetCurrentUserId(user),
                HasPermission(user, QmsPermissions.QualityInspect),
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("GetPanelQualityInspection");

        quality.MapGet("/action-departments", async (
            QualityInspectionStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListActionDepartmentsAsync(cancellationToken)))
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("ListPanelQualityActionDepartments");

        quality.MapPost("/reconcile", async (
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var actorId = GetCurrentUserId(user);
            return actorId is null
                ? Results.Unauthorized()
                : Results.Ok(await store.ReconcileHandoffsAsync(
                    actorId.Value,
                    GetProjectAccessScope(user),
                    cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("ReconcilePanelQualityInspectionHandoffs");

        quality.MapPost("/start", async (
            StartQualityInspectionRequest request,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await MutateQuality(user, (actorId, scope) => store.StartAsync(request, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("StartPanelQualityInspection");

        quality.MapPut("/reports/{reportId:guid}/responses", async (
            Guid reportId,
            SaveQualityInspectionResponsesRequest request,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await MutateQuality(user, (actorId, scope) => store.SaveResponsesAsync(reportId, request, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("SavePanelQualityInspectionResponses");

        quality.MapPost("/reports/{reportId:guid}/photos", async (
            Guid reportId,
            [FromForm] Guid operationId,
            [FromForm] Guid templateItemId,
            [FromForm] int expectedReportVersion,
            [FromForm] string altText,
            [FromForm] IFormFile photo,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var actorId = GetCurrentUserId(user);
            if (actorId is null) return Results.Unauthorized();
            if (photo.Length is < 1 or > 5 * 1024 * 1024)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["photo"] = ["사진은 5MB 이하 JPEG 또는 PNG 파일이어야 합니다."]
                });
            }
            await using var stream = photo.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            return ToResult(await store.AddPhotoAsync(
                reportId, operationId, templateItemId, expectedReportVersion, altText,
                memory.ToArray(), actorId.Value, GetProjectAccessScope(user), cancellationToken));
        })
        .WithMetadata(new RequestSizeLimitAttribute(6 * 1024 * 1024))
        .DisableAntiforgery()
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("UploadPanelQualityInspectionPhoto");

        quality.MapDelete("/reports/{reportId:guid}/photos/{photoId:guid}", async (
            Guid reportId,
            Guid photoId,
            Guid operationId,
            int? expectedReportVersion,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var actorId = GetCurrentUserId(user);
            return actorId is null
                ? Results.Unauthorized()
                : ToResult(await store.RemovePhotoAsync(
                    reportId, photoId, operationId, expectedReportVersion,
                    actorId.Value, GetProjectAccessScope(user), cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("DeletePanelQualityInspectionPhoto");

        quality.MapPost("/reports/{reportId:guid}/finalize", async (
            Guid reportId,
            FinalizeQualityInspectionRequest request,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
            await MutateQuality(user, (actorId, scope) => store.FinalizeAsync(
                reportId, request, actorId, scope, context.TraceIdentifier, cancellationToken)))
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("FinalizePanelQualityInspection");

        quality.MapPost("/reinspection", async (
            RequestQualityReinspectionRequest request,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await MutateQuality(user, (actorId, scope) => store.RequestReinspectionAsync(request, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("RequestPanelQualityReinspection");

        quality.MapPost("/manufacturing-completed/confirm", async (
            ConfirmPanelManufacturingCompletedRequest request,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await MutateQuality(user, (actorId, scope) => store.ConfirmManufacturingCompletedAsync(request, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithName("ConfirmPanelManufacturingCompleted");

        quality.MapGet("/reports/{reportId:guid}/photos/{photoId:guid}/content", async (
            Guid reportId,
            Guid photoId,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await store.GetPhotoContentAsync(reportId, photoId, GetProjectAccessScope(user), cancellationToken);
            if (result.Status == QualityInspectionMutationStatus.NotFound || result.Value is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(result.Value.Content, result.Value.NormalizedMime, result.Value.DisplayName);
        })
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("DownloadPanelQualityInspectionPhoto");

        quality.MapGet("/reports/{reportId:guid}/pdf", async (
            Guid reportId,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await store.GetPdfAsync(reportId, GetProjectAccessScope(user), cancellationToken);
            if (result.Status == QualityInspectionMutationStatus.NotFound || result.Value is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            return result.Value.Status switch
            {
                "Ready" when result.Value.Content is not null
                    => Results.File(result.Value.Content, "application/pdf", "panel-quality-report.pdf"),
                "Pending" => Results.Json(new { status = "Pending" }, statusCode: StatusCodes.Status202Accepted),
                _ => Results.Problem(title: "PDF를 생성하지 못했습니다.", statusCode: StatusCodes.Status409Conflict)
            };
        })
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("DownloadPanelQualityInspectionPdf");

        quality.MapPost("/reports/{reportId:guid}/pdf/retry", async (
            Guid reportId,
            RetryQualityInspectionPdfRequest request,
            QualityInspectionStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            await MutateQuality(user, (actorId, scope) => store.RetryPdfAsync(
                reportId, request.OperationId, actorId, scope, cancellationToken)))
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("RetryPanelQualityInspectionPdf");

        return app;
    }

    private static async Task<IResult> MutateQuality(
        ClaimsPrincipal user,
        Func<Guid, ProjectAccessScope, Task<QualityInspectionMutationResult<QualityInspectionMutationResponse>>> mutation)
    {
        var actorId = GetCurrentUserId(user);
        return actorId is null ? Results.Unauthorized() : ToResult(await mutation(actorId.Value, GetProjectAccessScope(user)));
    }

    private static IResult ToResult(QualityInspectionMutationResult<QualityInspectionMutationResponse> result)
    {
        return result.Status switch
        {
            QualityInspectionMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
            QualityInspectionMutationStatus.NotFound => Results.NotFound(),
            QualityInspectionMutationStatus.Validation => Results.ValidationProblem(result.Errors),
            QualityInspectionMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 품질검사 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static ProjectAccessScope GetProjectAccessScope(ClaimsPrincipal user)
        => new(
            user.HasClaim(QmsClaimTypes.Permission, QmsPermissions.ProjectReadAll),
            user.FindAll(QmsClaimTypes.Project).Select(claim => claim.Value).ToList());

    private static bool HasPermission(ClaimsPrincipal user, string permission)
        => user.HasClaim(QmsClaimTypes.Permission, permission);
}
