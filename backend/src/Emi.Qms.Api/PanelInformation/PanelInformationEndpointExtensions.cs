using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.PanelInformation;

public static class PanelInformationEndpointExtensions
{
    public static IEndpointRouteBuilder MapPanelInformationEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/projects/{projectId:guid}/panel-information");

        api.MapGet("/", async (
            Guid projectId,
            ProjectStore projectStore,
            PanelInformationStore panelInformationStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var response = await panelInformationStore.GetPanelInformationAsync(projectId, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("GetPanelInformation");

        api.MapPatch("/", async (
            Guid projectId,
            PanelInformationBulkUpdateRequest request,
            ProjectStore projectStore,
            PanelInformationStore panelInformationStore,
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

            var (input, validation) = PanelInformationRequestValidator.ValidateBulkUpdate(request);
            if (validation.HasErrors || input is null)
            {
                return Results.ValidationProblem(validation.Errors);
            }

            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await panelInformationStore.UpdatePanelInformationAsync(
                projectId,
                input,
                userId.Value,
                httpContext.TraceIdentifier,
                cancellationToken);

            if (result.Status == PanelInformationMutationStatus.Success && result.Value is not null)
            {
                await workflowStore.SyncStageWorkItemsAfterSaveAsync(
                    projectId,
                    WorkflowStageCodes.DesignPanelInfo,
                    "Panel",
                    projectId,
                    userId.Value,
                    httpContext.TraceIdentifier,
                    "패널명·사이즈 입력 완료",
                    cancellationToken);
            }

            return ToPanelInformationResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.PanelInfoUpdate)
        .WithName("UpdatePanelInformation");

        api.MapGet("/history", async (
            Guid projectId,
            ProjectStore projectStore,
            PanelInformationStore panelInformationStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var history = await panelInformationStore.GetHistoryAsync(projectId, cancellationToken);
            return history is null ? Results.NotFound() : Results.Ok(history);
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("GetPanelInformationHistory");

        api.MapPost("/import/preview", async (
            Guid projectId,
            HttpRequest request,
            ProjectStore projectStore,
            PanelInformationStore panelInformationStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var upload = await ReadExcelUploadAsync(request, cancellationToken);
            if (upload.Errors.Count > 0 || upload.File is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["File"] = upload.Errors.ToArray() });
            }

            var result = await panelInformationStore.PreviewExcelImportAsync(
                projectId,
                upload.File,
                upload.InputUnit,
                cancellationToken);

            return ToPanelInformationResult(result, Results.Ok);
        })
        .WithMetadata(new RequestSizeLimitAttribute(PanelInformationDomain.MaxExcelMultipartRequestBytes))
        .RequireAuthorization()
        .WithName("PreviewPanelInformationExcelImport");

        api.MapGet("/import/template", async (
            Guid projectId,
            string? unit,
            ProjectStore projectStore,
            PanelInformationStore panelInformationStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var result = await panelInformationStore.CreateTemplateAsync(projectId, unit, cancellationToken);
            return ToPanelInformationResult(
                result,
                template => Results.File(template.Content, template.ContentType, template.FileName));
        })
        .RequireAuthorization(QmsPolicies.PanelInfoUpdate)
        .WithName("DownloadPanelInformationExcelTemplate");

        api.MapPost("/import/apply", async (
            Guid projectId,
            HttpRequest request,
            ProjectStore projectStore,
            PanelInformationStore panelInformationStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

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

            var result = await panelInformationStore.ApplyExcelImportAsync(
                projectId,
                upload.File,
                upload.InputUnit,
                upload.ExpectedFileSha256,
                upload.ExpectedPackagingMethod,
                upload.Reason,
                PanelInformationStore.ParseExpectedVersions(upload.ExpectedVersions),
                userId.Value,
                httpContext.TraceIdentifier,
                cancellationToken);

            return ToPanelInformationResult(result, Results.Ok);
        })
        .WithMetadata(new RequestSizeLimitAttribute(PanelInformationDomain.MaxExcelMultipartRequestBytes))
        .RequireAuthorization(QmsPolicies.PanelInfoUpdate)
        .WithName("ApplyPanelInformationExcelImport");

        return app;
    }

    private static async Task<ExcelUploadForm> ReadExcelUploadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return new ExcelUploadForm(null, null, null, null, null, null, ["multipart/form-data 요청이 필요합니다."]);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return new ExcelUploadForm(null, null, null, null, null, null, ["file 필드가 필요합니다."]);
        }

        var fileValidation = PanelInformationExcelParser.ValidateUploadMetadata(file);
        if (fileValidation.Count > 0)
        {
            return new ExcelUploadForm(null, null, null, null, null, null, fileValidation.ToArray());
        }

        var uploadedFile = await PanelInformationExcelParser.ReadUploadedFileAsync(file, cancellationToken);
        return new ExcelUploadForm(
            uploadedFile,
            form["inputUnit"].ToString(),
            form["expectedFileSha256"].ToString(),
            form["expectedPackagingMethod"].ToString(),
            form["reason"].ToString(),
            form["expectedVersions"].ToString(),
            []);
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

        if (!CanAccessProject(user, accessRecord.ProjectKey))
        {
            return Results.Forbid();
        }

        return null;
    }

    private static bool CanAccessProject(ClaimsPrincipal user, string projectKey)
    {
        return HasPermission(user, QmsPermissions.ProjectReadAll)
            || user.FindAll(QmsClaimTypes.Project).Any(claim => string.Equals(claim.Value, projectKey, StringComparison.Ordinal));
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

    private static IResult ToPanelInformationResult<T>(
        PanelInformationMutationResult<T> result,
        Func<T, IResult> success)
    {
        return result.Status switch
        {
            PanelInformationMutationStatus.Success when result.Value is not null => success(result.Value),
            PanelInformationMutationStatus.NotFound => Results.NotFound(),
            PanelInformationMutationStatus.Forbidden => Results.Forbid(),
            PanelInformationMutationStatus.ValidationFailed => Results.ValidationProblem(result.Errors ?? new Dictionary<string, string[]>()),
            PanelInformationMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private sealed record ExcelUploadForm(
        UploadedExcelFile? File,
        string? InputUnit,
        string? ExpectedFileSha256,
        string? ExpectedPackagingMethod,
        string? Reason,
        string? ExpectedVersions,
        IReadOnlyList<string> Errors);
}
