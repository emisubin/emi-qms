using System.Security.Claims;
using System.Security.Cryptography;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.ProductionPlanning;

public static class ProductionPlanningEndpointExtensions
{
    public static IEndpointRouteBuilder MapProductionPlanningEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/production-planning/summary", async (
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            return HasPermission(user, QmsPermissions.ProjectRead)
                ? Results.Ok(await store.GetSummaryAsync(cancellationToken))
                : Results.Forbid();
        })
        .RequireAuthorization()
        .WithName("GetProductionPlanningSummary");

        app.MapGet("/api/production-planning/projects", async (
            string? search,
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            return HasPermission(user, QmsPermissions.ProjectRead)
                ? Results.Ok(await store.ListProjectsAsync(search, cancellationToken))
                : Results.Forbid();
        })
        .RequireAuthorization()
        .WithName("ListProductionPlanningProjects");

        app.MapGet("/api/production-planning/product-types", async (
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            return HasPermission(user, QmsPermissions.ProjectRead)
                ? Results.Ok(await store.ListProductTypesAsync(cancellationToken))
                : Results.Forbid();
        })
        .RequireAuthorization()
        .WithName("ListProductionProductTypes");

        app.MapPost("/api/production-planning/product-types", async (
            UpsertProductionProductTypeRequest request,
            ProductionPlanningStore store,
            CancellationToken cancellationToken) =>
        {
            var result = await store.CreateProductTypeAsync(request, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.ProductionPlanUpdate)
        .WithName("CreateProductionProductType");

        app.MapGet("/api/production-planning/settings/templates", async (
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            return HasPermission(user, QmsPermissions.ProjectRead)
                ? Results.Ok(await store.ListTemplateSettingsAsync(cancellationToken))
                : Results.Forbid();
        })
        .RequireAuthorization()
        .WithName("ListProductionTemplateSettings");

        app.MapPatch("/api/production-planning/settings/templates/{productTypeId:guid}", async (
            Guid productTypeId,
            UpdateProductionTemplateSettingsRequest request,
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.UpdateTemplateSettingsAsync(productTypeId, request, userId.Value, httpContext.TraceIdentifier, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.ProductionPlanUpdate)
        .WithName("UpdateProductionTemplateSettings");

        app.MapGet("/api/system/holidays", async (
            string? countryCode,
            DateOnly? dateFrom,
            DateOnly? dateTo,
            SystemHolidayStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            return HasPermission(user, QmsPermissions.ProjectRead)
                ? Results.Ok(await store.ListAsync(countryCode, dateFrom, dateTo, cancellationToken))
                : Results.Forbid();
        })
        .RequireAuthorization()
        .WithName("ListSystemHolidays");

        app.MapPost("/api/system/holidays/sync/kr", async (
            SyncKoreanHolidaysRequest request,
            SystemHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await store.SyncKoreanHolidaysAsync(request.Year, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("SyncKoreanHolidays");

        app.MapPost("/api/production-planning/holidays/sync", async (
            int? year,
            SystemHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await store.SyncKoreanHolidaysAsync(year, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("SyncProductionPlanningKoreanHolidays");

        app.MapGet("/api/production-planning/import/template", async (
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var template = await store.CreateBulkTemplateAsync(cancellationToken);
            return Results.File(template.Content, template.ContentType, template.FileName);
        })
        .RequireAuthorization(QmsPolicies.ProductionPlanUpdate)
        .WithName("DownloadProductionPlanningBulkTemplate");

        app.MapPost("/api/production-planning/import/preview", async (
            [FromForm] IFormFile file,
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var uploaded = await ReadUploadedExcelAsync(file, cancellationToken);
            if (uploaded.Error is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [uploaded.Error] });
            }

            var result = await store.PreviewBulkExcelAsync(uploaded.FileName!, uploaded.Bytes!, uploaded.Sha256!, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.ProductionPlanUpdate)
        .DisableAntiforgery()
        .WithName("PreviewProductionPlanningBulkExcel");

        app.MapPost("/api/production-planning/import/apply", async (
            [FromForm] IFormFile file,
            [FromForm] string expectedFileSha256,
            [FromForm] string? reason,
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var uploaded = await ReadUploadedExcelAsync(file, cancellationToken);
            if (uploaded.Error is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [uploaded.Error] });
            }

            var result = await store.ApplyBulkExcelAsync(
                uploaded.FileName!,
                uploaded.Bytes!,
                uploaded.Sha256!,
                expectedFileSha256,
                reason,
                userId.Value,
                httpContext.TraceIdentifier,
                cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.ProductionPlanUpdate)
        .DisableAntiforgery()
        .WithName("ApplyProductionPlanningBulkExcel");

        var projectApi = app.MapGroup("/api/projects/{projectId:guid}/production-planning");

        projectApi.MapGet("/", async (
            Guid projectId,
            ProjectStore projectStore,
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var plan = await store.GetProjectPlanAsync(projectId, cancellationToken);
            return plan is null ? Results.NotFound() : Results.Ok(plan);
        })
        .RequireAuthorization()
        .WithName("GetProjectProductionPlanning");

        projectApi.MapPatch("/", async (
            Guid projectId,
            UpdateProductionPlanningRequest request,
            ProjectStore projectStore,
            ProductionPlanningStore store,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.UpdateProjectPlanAsync(
                projectId,
                request,
                userId.Value,
                httpContext.TraceIdentifier,
                cancellationToken);

            if (result.Status == ProductionPlanningMutationStatus.Success && result.Value is not null)
            {
                await workflowStore.CompleteStageAsync(
                    projectId,
                    WorkflowStageCodes.ProductionPlanning,
                    "ProductionPlan",
                    result.Value.PlanId,
                    userId.Value,
                    httpContext.TraceIdentifier,
                    "생산계획 저장 완료",
                    cancellationToken);
                await workflowStore.GenerateProductionPlanningAssigneeFollowUpsAsync(
                    projectId,
                    userId.Value,
                    httpContext.TraceIdentifier,
                    cancellationToken);
            }

            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.ProductionPlanUpdate)
        .WithName("UpdateProjectProductionPlanning");

        projectApi.MapGet("/history", async (
            Guid projectId,
            ProjectStore projectStore,
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var history = await store.GetHistoryAsync(projectId, cancellationToken);
            return history is null ? Results.NotFound() : Results.Ok(history);
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("GetProjectProductionPlanningHistory");

        projectApi.MapGet("/export-template", async (
            Guid projectId,
            Guid productTypeId,
            ProjectStore projectStore,
            ProductionPlanningStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var template = await store.CreateTemplateAsync(projectId, productTypeId, cancellationToken);
            return template is null
                ? Results.NotFound()
                : Results.File(template.Content, template.ContentType, template.FileName);
        })
        .RequireAuthorization(QmsPolicies.ProductionPlanUpdate)
        .WithName("DownloadProductionPlanningTemplate");

        return app;
    }

    private static async Task<IResult?> AuthorizeProjectReadAsync(
        ProjectStore projectStore,
        ClaimsPrincipal user,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(user, QmsPermissions.ProjectRead))
        {
            return Results.Forbid();
        }

        var accessRecord = await projectStore.GetProjectAccessRecordAsync(projectId, cancellationToken);
        if (accessRecord is null)
        {
            return Results.NotFound();
        }

        if (!HasPermission(user, QmsPermissions.ProjectReadAll)
            && !user.FindAll(QmsClaimTypes.Project).Any(claim => string.Equals(claim.Value, accessRecord.ProjectKey, StringComparison.Ordinal)))
        {
            return Results.Forbid();
        }

        return null;
    }

    private static IResult ToResult<T>(ProductionPlanningMutationResult<T> result, Func<T, IResult> success)
    {
        return result.Status switch
        {
            ProductionPlanningMutationStatus.Success when result.Value is not null => success(result.Value),
            ProductionPlanningMutationStatus.NotFound => Results.NotFound(),
            ProductionPlanningMutationStatus.Validation => Results.ValidationProblem(result.Errors),
            ProductionPlanningMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static bool HasPermission(ClaimsPrincipal user, string permissionCode)
    {
        return user.Identity?.IsAuthenticated == true
            && user.HasClaim(QmsClaimTypes.Permission, permissionCode);
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static async Task<(string? FileName, byte[]? Bytes, string? Sha256, string? Error)> ReadUploadedExcelAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return (null, null, null, "선택한 파일이 비어 있습니다.");
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return (null, null, null, "Excel 파일은 10MB 이하만 업로드할 수 있습니다.");
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, null, ".xlsx 파일만 업로드할 수 있습니다.");
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return (Path.GetFileName(file.FileName), bytes, hash, null);
    }
}
