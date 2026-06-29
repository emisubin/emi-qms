using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.PanelInformation;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.Projects;

public static class ProjectEndpointExtensions
{
    private static readonly IReadOnlySet<string> ActiveOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        "Active"
    };

    private static readonly IReadOnlySet<string> OnHoldOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        "OnHold"
    };

    private static readonly IReadOnlySet<string> ActiveOrOnHold = new HashSet<string>(StringComparer.Ordinal)
    {
        "Active",
        "OnHold"
    };

    private static readonly IReadOnlySet<string> CancelledOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        "Cancelled"
    };

    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/sales-owners", async (
            ProjectStore projectStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var owners = await projectStore.GetSalesOwnersAsync(cancellationToken);
            return Results.Ok(owners);
        })
        .RequireAuthorization()
        .WithName("GetSalesOwners");

        api.MapGet("/projects", async (
            HttpRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var dateRange = ParseDateRange(request, "deliveryDateFrom", "deliveryDateTo");
            if (dateRange.Errors.Count > 0)
            {
                return Results.ValidationProblem(dateRange.Errors);
            }

            var query = ParseProjectListQuery(request);
            var result = await projectStore.ListProjectsAsync(
                query,
                GetProjectAccessScope(user),
                CanReadSalesAmount(user),
                cancellationToken);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ListProjects");

        api.MapGet("/projects/summary", async (
            ProjectStore projectStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var result = await projectStore.GetProjectDashboardSummaryAsync(
                GetProjectAccessScope(user),
                cancellationToken);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetProjectDashboardSummary");

        api.MapGet("/deleted-projects", async (
            HttpRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var result = await projectStore.ListDeletedProjectsAsync(
                ParseDeletedProjectListQuery(request),
                CanReadSalesAmount(user),
                cancellationToken);

            return Results.Ok(result);
        })
        .RequireAuthorization(QmsPolicies.ProjectDeletedRead)
        .WithName("ListDeletedProjects");

        api.MapGet("/deleted-projects/{projectId:guid}", async (
            Guid projectId,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var project = await projectStore.GetDeletedProjectAsync(
                projectId,
                CanReadSalesAmount(user),
                HasPermission(user, QmsPermissions.AuditReadAll),
                cancellationToken);
            return project is null ? Results.NotFound() : Results.Ok(project);
        })
        .RequireAuthorization(QmsPolicies.ProjectDeletedRead)
        .WithName("GetDeletedProject");

        api.MapPost("/deleted-projects/{projectId:guid}/restore", async (
            Guid projectId,
            RestoreDeletedProjectRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await projectStore.RestoreDeletedProjectAsync(
                projectId,
                request.Reason,
                userId.Value,
                httpContext.TraceIdentifier,
                CanReadSalesAmount(user),
                cancellationToken);
            return ToProjectMutationResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("RestoreDeletedProject");

        api.MapDelete("/deleted-projects/{projectId:guid}/purge", async (
            Guid projectId,
            [FromBody] PurgeDeletedProjectRequest request,
            ProjectStore projectStore,
            CancellationToken cancellationToken) =>
        {
            var result = await projectStore.PurgeDeletedProjectAsync(projectId, request.ConfirmText, cancellationToken);
            return ToProjectMutationResult(result, value => Results.Ok(value));
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("PurgeDeletedProject");

        api.MapPost("/deleted-projects/purge-all", async (
            PurgeDeletedProjectRequest request,
            ProjectStore projectStore,
            CancellationToken cancellationToken) =>
        {
            var result = await projectStore.PurgeAllDeletedProjectsAsync(request.ConfirmText, cancellationToken);
            return ToProjectMutationResult(result, value => Results.Ok(value));
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("PurgeAllDeletedProjects");

        api.MapPost("/projects", async (
            CreateProjectRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (input, validation) = ProjectRequestValidator.ValidateCreate(request);
            if (validation.HasErrors || input is null)
            {
                return Results.ValidationProblem(validation.Errors);
            }

            if (!await projectStore.IsActiveProductionProductTypeCodeAsync(input.Item, cancellationToken))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(CreateProjectRequest.Item)] = ["Item은 등록된 Item 기준값 중 하나여야 합니다."]
                });
            }

            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await projectStore.CreateProjectAsync(
                input,
                userId.Value,
                httpContext.TraceIdentifier,
                CanReadSalesAmount(user),
                cancellationToken);

            return ToProjectMutationResult(result, value => Results.Created($"/api/projects/{value.ProjectId}", value));
        })
        .RequireAuthorization(QmsPolicies.ProjectCreate)
        .WithName("CreateProject");

        api.MapGet("/projects/import/template", (
            ProjectStore projectStore) =>
        {
            var template = projectStore.CreateProjectExcelTemplate();
            return Results.File(template.Content, template.ContentType, template.FileName);
        })
        .RequireAuthorization(QmsPolicies.ProjectCreate)
        .WithName("DownloadProjectExcelTemplate");

        api.MapPost("/projects/import/preview", async (
            HttpRequest request,
            ProjectStore projectStore,
            CancellationToken cancellationToken) =>
        {
            var upload = await ReadProjectExcelUploadAsync(request, cancellationToken);
            if (upload.Errors.Count > 0 || upload.File is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["File"] = upload.Errors.ToArray() });
            }

            var result = await projectStore.PreviewProjectExcelAsync(upload.File, cancellationToken);
            return ToProjectMutationResult(result, Results.Ok);
        })
        .WithMetadata(new RequestSizeLimitAttribute(ProjectExcelParser.MaxExcelMultipartRequestBytes))
        .RequireAuthorization(QmsPolicies.ProjectCreate)
        .WithName("PreviewProjectExcelImport");

        api.MapPost("/projects/import/apply", async (
            HttpRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var upload = await ReadProjectExcelUploadAsync(request, cancellationToken);
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

            var result = await projectStore.ApplyProjectExcelAsync(
                upload.File,
                upload.ExpectedFileSha256,
                userId.Value,
                httpContext.TraceIdentifier,
                cancellationToken);
            return ToProjectMutationResult(result, Results.Ok);
        })
        .WithMetadata(new RequestSizeLimitAttribute(ProjectExcelParser.MaxExcelMultipartRequestBytes))
        .RequireAuthorization(QmsPolicies.ProjectCreate)
        .WithName("ApplyProjectExcelImport");

        api.MapGet("/projects/{projectId:guid}", async (
            Guid projectId,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var project = await projectStore.GetProjectAsync(projectId, CanReadSalesAmount(user), cancellationToken);
            return project is null ? Results.NotFound() : Results.Ok(project);
        })
        .RequireAuthorization()
        .WithName("GetProject");

        api.MapPatch("/projects/{projectId:guid}", async (
            Guid projectId,
            UpdateProjectRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (input, validation) = ProjectRequestValidator.ValidateUpdate(request);
            if (validation.HasErrors || input is null)
            {
                return Results.ValidationProblem(validation.Errors);
            }

            if (!await projectStore.IsActiveProductionProductTypeCodeAsync(input.Item, cancellationToken))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(UpdateProjectRequest.Item)] = ["Item은 등록된 Item 기준값 중 하나여야 합니다."]
                });
            }

            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await projectStore.UpdateProjectAsync(
                projectId,
                input,
                userId.Value,
                httpContext.TraceIdentifier,
                CanReadSalesAmount(user),
                cancellationToken);

            return ToProjectMutationResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.ProjectUpdate)
        .WithName("UpdateProject");

        api.MapPost("/projects/{projectId:guid}/change-panel-count", async (
            Guid projectId,
            ChangePanelCountRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (input, validation) = ProjectRequestValidator.ValidatePanelCountChange(request);
            if (validation.HasErrors || input is null)
            {
                return Results.ValidationProblem(validation.Errors);
            }

            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await projectStore.ChangePanelCountAsync(
                projectId,
                input,
                userId.Value,
                httpContext.TraceIdentifier,
                CanReadSalesAmount(user),
                cancellationToken);

            return ToProjectMutationResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.ProjectUpdate)
        .WithName("ChangeProjectPanelCount");

        api.MapPost("/projects/{projectId:guid}/hold", async (
            Guid projectId,
            ProjectStatusChangeRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            return await ChangeStatusAsync(
                projectId,
                request,
                projectStore,
                user,
                httpContext,
                "ProjectHeld",
                "OnHold",
                ActiveOnly,
                cancellationToken);
        })
        .RequireAuthorization(QmsPolicies.ProjectHold)
        .WithName("HoldProject");

        api.MapPost("/projects/{projectId:guid}/resume", async (
            Guid projectId,
            ProjectStatusChangeRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            return await ChangeStatusAsync(
                projectId,
                request,
                projectStore,
                user,
                httpContext,
                "ProjectResumed",
                "Active",
                OnHoldOnly,
                cancellationToken);
        })
        .RequireAuthorization(QmsPolicies.ProjectUpdate)
        .WithName("ResumeProject");

        api.MapPost("/projects/{projectId:guid}/cancel", async (
            Guid projectId,
            ProjectStatusChangeRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            return await ChangeStatusAsync(
                projectId,
                request,
                projectStore,
                user,
                httpContext,
                "ProjectCancelled",
                "Cancelled",
                ActiveOrOnHold,
                cancellationToken);
        })
        .RequireAuthorization(QmsPolicies.ProjectCancel)
        .WithName("CancelProject");

        api.MapPost("/projects/{projectId:guid}/reactivate", async (
            Guid projectId,
            ProjectStatusChangeRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            return await ChangeStatusAsync(
                projectId,
                request,
                projectStore,
                user,
                httpContext,
                "ProjectReactivated",
                "Active",
                CancelledOnly,
                cancellationToken);
        })
        .RequireAuthorization(QmsPolicies.ProjectUpdate)
        .WithName("ReactivateProject");

        api.MapPost("/projects/{projectId:guid}/delete", async (
            Guid projectId,
            DeleteProjectRequest request,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (input, validation) = ProjectRequestValidator.ValidateDelete(request);
            if (validation.HasErrors || input is null)
            {
                return Results.ValidationProblem(validation.Errors);
            }

            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await projectStore.DeleteProjectAsync(
                projectId,
                input,
                userId.Value,
                httpContext.TraceIdentifier,
                CanReadSalesAmount(user),
                cancellationToken);

            return ToProjectMutationResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.ProjectDelete)
        .WithName("DeleteProject");

        api.MapGet("/projects/{projectId:guid}/panels", async (
            Guid projectId,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var panels = await projectStore.ListPanelsAsync(projectId, cancellationToken);
            return panels is null ? Results.NotFound() : Results.Ok(panels);
        })
        .RequireAuthorization()
        .WithName("ListProjectPanels");

        api.MapGet("/projects/{projectId:guid}/panels/{panelId:guid}", async (
            Guid projectId,
            Guid panelId,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var panel = await projectStore.GetPanelAsync(projectId, panelId, cancellationToken);
            return panel is null ? Results.NotFound() : Results.Ok(panel);
        })
        .RequireAuthorization()
        .WithName("GetProjectPanel");

        api.MapGet("/projects/{projectId:guid}/audit-history", async (
            Guid projectId,
            ProjectStore projectStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var history = await projectStore.GetAuditHistoryAsync(projectId, CanReadSalesAmount(user), cancellationToken);
            return history is null ? Results.NotFound() : Results.Ok(history);
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("GetProjectAuditHistory");

        return app;
    }

    private static async Task<IResult?> ChangeStatusAsync(
        Guid projectId,
        ProjectStatusChangeRequest request,
        ProjectStore projectStore,
        ClaimsPrincipal user,
        HttpContext httpContext,
        string action,
        string targetStatus,
        IReadOnlySet<string> allowedSourceStatuses,
        CancellationToken cancellationToken)
    {
        var (reason, validation) = ProjectRequestValidator.ValidateReason(request);
        if (validation.HasErrors || reason is null)
        {
            return Results.ValidationProblem(validation.Errors);
        }

        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var result = await projectStore.ChangeStatusAsync(
            projectId,
            action,
            targetStatus,
            allowedSourceStatuses,
            reason,
            userId.Value,
            httpContext.TraceIdentifier,
            CanReadSalesAmount(user),
            cancellationToken);

        return ToProjectMutationResult(result, Results.Ok);
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

    private static ProjectListQuery ParseProjectListQuery(HttpRequest request)
    {
        return new ProjectListQuery(
            request.Query["search"].ToString(),
            request.Query["status"].ToString(),
            TryParseGuid(request.Query["salesOwnerUserId"].ToString()),
            TryParseDate(request.Query["deliveryDateFrom"].ToString()),
            TryParseDate(request.Query["deliveryDateTo"].ToString()),
            bool.TryParse(request.Query["includeCancelled"].ToString(), out var includeCancelled) && includeCancelled,
            int.TryParse(request.Query["page"].ToString(), out var page) ? page : 1,
            int.TryParse(request.Query["pageSize"].ToString(), out var pageSize) ? pageSize : 20);
    }

    private static DeletedProjectListQuery ParseDeletedProjectListQuery(HttpRequest request)
    {
        return new DeletedProjectListQuery(
            request.Query["search"].ToString(),
            TryParseGuid(request.Query["deletedByUserId"].ToString()),
            TryParseDateTimeOffset(request.Query["deletedAtFrom"].ToString()),
            TryParseDateTimeOffset(request.Query["deletedAtTo"].ToString()),
            int.TryParse(request.Query["page"].ToString(), out var page) ? page : 1,
            int.TryParse(request.Query["pageSize"].ToString(), out var pageSize) ? pageSize : 20);
    }

    private static async Task<ProjectExcelUploadForm> ReadProjectExcelUploadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return new ProjectExcelUploadForm(null, null, ["multipart/form-data 요청이 필요합니다."]);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return new ProjectExcelUploadForm(null, null, ["file 필드가 필요합니다."]);
        }

        var validation = ProjectExcelParser.ValidateUploadMetadata(file);
        if (validation.Count > 0)
        {
            return new ProjectExcelUploadForm(null, null, validation.ToArray());
        }

        var uploadedFile = await ProjectExcelParser.ReadUploadedFileAsync(file, cancellationToken);
        return new ProjectExcelUploadForm(
            uploadedFile,
            form["expectedFileSha256"].ToString(),
            []);
    }

    private static ProjectDateRange ParseDateRange(HttpRequest request, string fromKey, string toKey)
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

        return new ProjectDateRange(from, to, errors);
    }

    private static IResult ToProjectMutationResult<T>(
        ProjectMutationResult<T> result,
        Func<T, IResult> success)
    {
        return result.Status switch
        {
            ProjectMutationStatus.Success when result.Value is not null => success(result.Value),
            ProjectMutationStatus.NotFound => Results.NotFound(),
            ProjectMutationStatus.ValidationFailed => Results.ValidationProblem(result.Errors ?? new Dictionary<string, string[]>()),
            ProjectMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static ProjectAccessScope GetProjectAccessScope(ClaimsPrincipal user)
    {
        return new ProjectAccessScope(
            HasPermission(user, QmsPermissions.ProjectReadAll),
            user.FindAll(QmsClaimTypes.Project).Select(claim => claim.Value).ToList());
    }

    private static bool CanAccessProject(ClaimsPrincipal user, string projectKey)
    {
        return HasPermission(user, QmsPermissions.ProjectReadAll)
            || user.FindAll(QmsClaimTypes.Project).Any(claim => string.Equals(claim.Value, projectKey, StringComparison.Ordinal));
    }

    private static bool CanReadSalesAmount(ClaimsPrincipal user)
    {
        return HasPermission(user, QmsPermissions.ProjectSalesAmountRead);
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

    private static Guid? TryParseGuid(string? value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static DateOnly? TryParseDate(string? value)
    {
        return DateOnly.TryParse(value, out var parsed) ? parsed : null;
    }

    private static DateTimeOffset? TryParseDateTimeOffset(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private sealed record ProjectExcelUploadForm(
        UploadedExcelFile? File,
        string? ExpectedFileSha256,
        IReadOnlyList<string> Errors);

    private sealed record ProjectDateRange(
        DateOnly? From,
        DateOnly? To,
        IReadOnlyDictionary<string, string[]> Errors);
}
