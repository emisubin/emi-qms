using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.PanelInformation;
using Emi.Qms.Api.Projects;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.Procurement;

public static class ProcurementEndpointExtensions
{
    public static IEndpointRouteBuilder MapProcurementEndpoints(this IEndpointRouteBuilder app)
    {
        var projectApi = app.MapGroup("/api/projects/{projectId:guid}/procurement");

        projectApi.MapGet("/", async (
            Guid projectId,
            ProjectStore projectStore,
            ProcurementStore procurementStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var response = await procurementStore.GetProjectProcurementAsync(projectId, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("GetProjectProcurement");

        projectApi.MapPatch("/", async (
            Guid projectId,
            ProcurementBulkUpdateRequest request,
            ProjectStore projectStore,
            ProcurementStore procurementStore,
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

            var result = await procurementStore.UpdateProjectProcurementAsync(
                projectId,
                request,
                userId.Value,
                httpContext.TraceIdentifier,
                cancellationToken);

            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.ProcurementPlanUpdate)
        .WithName("UpdateProjectProcurement");

        projectApi.MapGet("/history", async (
            Guid projectId,
            ProjectStore projectStore,
            ProcurementStore procurementStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var history = await procurementStore.GetHistoryAsync(projectId, cancellationToken);
            return history is null ? Results.NotFound() : Results.Ok(history);
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("GetProjectProcurementHistory");

        projectApi.MapGet("/import/template", async (
            Guid projectId,
            ProjectStore projectStore,
            ProcurementStore procurementStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var result = await procurementStore.CreateTemplateAsync(projectId, cancellationToken);
            return ToResult(result, template => Results.File(template.Content, template.ContentType, template.FileName));
        })
        .RequireAuthorization(QmsPolicies.ProcurementPlanUpdate)
        .WithName("DownloadProcurementExcelTemplate");

        app.MapPost("/api/procurement/import/preview", async (
            HttpRequest request,
            ProcurementStore procurementStore,
            CancellationToken cancellationToken) =>
        {
            var upload = await ReadExcelUploadAsync(request, cancellationToken);
            if (upload.Errors.Count > 0 || upload.File is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["File"] = upload.Errors.ToArray() });
            }

            var result = await procurementStore.PreviewExcelAsync(
                upload.File,
                ProcurementStore.ParseProjectSelections(upload.ProjectSelections),
                cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .WithMetadata(new RequestSizeLimitAttribute(ProcurementDomain.MaxExcelMultipartRequestBytes))
        .RequireAuthorization(QmsPolicies.ProcurementPlanUpdate)
        .WithName("PreviewProcurementExcelImport");

        app.MapPost("/api/procurement/import/apply", async (
            HttpRequest request,
            ProcurementStore procurementStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var upload = await ReadExcelUploadAsync(request, cancellationToken);
            if (upload.Errors.Count > 0 || upload.File is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["File"] = upload.Errors.ToArray() });
            }

            if (string.IsNullOrWhiteSpace(upload.ExpectedFileSha256))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["ExpectedFileSha256"] = ["미리보기 파일 해시가 필요합니다."] });
            }

            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await procurementStore.ApplyExcelAsync(
                upload.File,
                upload.ExpectedFileSha256,
                ProcurementStore.ParseProjectSelections(upload.ProjectSelections),
                ProcurementStore.ParseExpectedVersions(upload.ExpectedVersions),
                upload.Reason,
                userId.Value,
                httpContext.TraceIdentifier,
                cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .WithMetadata(new RequestSizeLimitAttribute(ProcurementDomain.MaxExcelMultipartRequestBytes))
        .RequireAuthorization(QmsPolicies.ProcurementPlanUpdate)
        .WithName("ApplyProcurementExcelImport");

        app.MapGet("/api/procurement/import/template", (
            ProcurementStore procurementStore) =>
        {
            var template = procurementStore.CreateTemplate();
            return Results.File(template.Content, template.ContentType, template.FileName);
        })
        .RequireAuthorization(QmsPolicies.ProcurementPlanUpdate)
        .WithName("DownloadProcurementDashboardExcelTemplate");

        app.MapGet("/api/procurement/dashboard", async (
            HttpRequest request,
            string? search,
            ProcurementStore procurementStore,
            CancellationToken cancellationToken) =>
        {
            var dateRange = ParseDateRange(request, "expectedReceiptDateFrom", "expectedReceiptDateTo");
            if (dateRange.Errors.Count > 0)
            {
                return Results.ValidationProblem(dateRange.Errors);
            }

            return Results.Ok(await procurementStore.GetProcurementDashboardAsync(
                search,
                dateRange.From,
                dateRange.To,
                cancellationToken));
        })
        .RequireAuthorization()
        .WithName("GetProcurementDashboard");

        var materialApi = app.MapGroup("/api/materials/receipts");
        materialApi.MapGet("/", async (
            HttpRequest request,
            string? search,
            bool? includeCompleted,
            ProcurementStore procurementStore,
            CancellationToken cancellationToken) =>
        {
            var dateRange = ParseDateRange(request, "expectedReceiptDateFrom", "expectedReceiptDateTo");
            if (dateRange.Errors.Count > 0)
            {
                return Results.ValidationProblem(dateRange.Errors);
            }

            return Results.Ok(await procurementStore.GetMaterialReceiptsAsync(
                search,
                includeCompleted == true,
                dateRange.From,
                dateRange.To,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("GetMaterialReceipts");

        materialApi.MapPatch("/", async (
            ProcurementReceiptBulkUpdateRequest request,
            ProcurementStore procurementStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await procurementStore.UpdateMaterialReceiptsAsync(
                request,
                userId.Value,
                httpContext.TraceIdentifier,
                cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("UpdateMaterialReceipts");

        return app;
    }

    private static async Task<ProcurementExcelUploadForm> ReadExcelUploadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return new ProcurementExcelUploadForm(null, null, null, null, null, ["multipart/form-data 요청이 필요합니다."]);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return new ProcurementExcelUploadForm(null, null, null, null, null, ["file 필드가 필요합니다."]);
        }

        var fileValidation = ProcurementExcelParser.ValidateUploadMetadata(file);
        if (fileValidation.Count > 0)
        {
            return new ProcurementExcelUploadForm(null, null, null, null, null, fileValidation.ToArray());
        }

        var uploadedFile = await ProcurementExcelParser.ReadUploadedFileAsync(file, cancellationToken);
        return new ProcurementExcelUploadForm(
            uploadedFile,
            form["expectedFileSha256"].ToString(),
            form["reason"].ToString(),
            form["projectSelections"].ToString(),
            form["expectedVersions"].ToString(),
            []);
    }

    private static async Task<IResult?> AuthorizeProjectReadAsync(ProjectStore projectStore, ClaimsPrincipal user, Guid projectId, CancellationToken cancellationToken)
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

    private static IResult ToResult<T>(ProcurementMutationResult<T> result, Func<T, IResult> success)
    {
        return result.Status switch
        {
            ProcurementMutationStatus.Success when result.Value is not null => success(result.Value),
            ProcurementMutationStatus.NotFound => Results.NotFound(),
            ProcurementMutationStatus.Forbidden => Results.Forbid(),
            ProcurementMutationStatus.Validation => Results.ValidationProblem(result.Errors),
            ProcurementMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static ProcurementDateRange ParseDateRange(HttpRequest request, string fromKey, string toKey)
    {
        var errors = new Dictionary<string, string[]>();
        var fromRaw = request.Query[fromKey].ToString();
        var toRaw = request.Query[toKey].ToString();
        DateOnly? from = null;
        DateOnly? to = null;

        if (!string.IsNullOrWhiteSpace(fromRaw))
        {
            if (DateOnly.TryParse(fromRaw, out var parsedFrom))
            {
                from = parsedFrom;
            }
            else
            {
                errors[fromKey] = ["올바른 날짜 형식이 아닙니다."];
            }
        }

        if (!string.IsNullOrWhiteSpace(toRaw))
        {
            if (DateOnly.TryParse(toRaw, out var parsedTo))
            {
                to = parsedTo;
            }
            else
            {
                errors[toKey] = ["올바른 날짜 형식이 아닙니다."];
            }
        }

        if (from is not null && to is not null && from > to)
        {
            errors[fromKey] = ["시작일은 종료일보다 늦을 수 없습니다."];
        }

        return new ProcurementDateRange(from, to, errors);
    }

    private sealed record ProcurementExcelUploadForm(
        UploadedExcelFile? File,
        string? ExpectedFileSha256,
        string? Reason,
        string? ProjectSelections,
        string? ExpectedVersions,
        IReadOnlyList<string> Errors);

    private sealed record ProcurementDateRange(
        DateOnly? From,
        DateOnly? To,
        IReadOnlyDictionary<string, string[]> Errors);
}
