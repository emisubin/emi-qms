using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.PanelInformation;
using Emi.Qms.Api.Projects;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.OsanProjects;

public static class OsanProjectEndpointExtensions
{
    public static IEndpointRouteBuilder MapOsanProjectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/osan/dashboard", async (
            HttpRequest request,
            DatabaseConnectionStringProvider connectionStringProvider,
            TimeProvider timeProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!IsSelectedOsan(connectionStringProvider))
            {
                return BusinessUnitDenied();
            }

            if (!ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var (query, errors) = ParseDashboardQuery(request.Query);
            if (query is null)
            {
                return Results.ValidationProblem(errors);
            }

            var response = await new OsanDashboardStore(connectionStringProvider, timeProvider).GetAsync(
                query,
                ProjectEndpointExtensions.GetProjectAccessScope(user),
                cancellationToken);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("GetOsanDashboard");

        var api = app.MapGroup("/api/osan/projects");

        api.MapGet("/import/template", (
            OsanProjectStore store,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user) =>
        {
            var denied = GuardImportAccess(connectionStringProvider, user);
            return denied ?? Results.File(
                store.CreateExcelTemplate(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "osan-project-import-template.xlsx");
        })
        .RequireAuthorization(QmsPolicies.ProjectCreate)
        .WithName("DownloadOsanProjectImportTemplate");

        api.MapPost("/import/preview", async (
            HttpRequest request,
            OsanProjectStore store,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var denied = GuardImportAccess(connectionStringProvider, user);
            if (denied is not null)
            {
                return denied;
            }

            var (form, errors) = await ReadExcelFormAsync(request, cancellationToken);
            if (form is null)
            {
                return Results.ValidationProblem(errors);
            }
            var (file, fileErrors) = await ReadExcelFileAsync(form, cancellationToken);
            foreach (var error in fileErrors) errors[error.Key] = error.Value;
            var rows = ReadExcelRows(form, errors);
            if (file is null || errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }
            return Results.Ok(await store.PreviewExcelAsync(file, rows, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.ProjectCreate)
        .WithMetadata(new RequestSizeLimitAttribute(OsanProjectExcelParser.MaximumMultipartBytes))
        .WithName("PreviewOsanProjectImport");

        api.MapPost("/import/apply", async (
            HttpRequest request,
            OsanProjectStore store,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var denied = GuardImportAccess(connectionStringProvider, user);
            if (denied is not null)
            {
                return denied;
            }

            var userId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var (form, formErrors) = await ReadExcelFormAsync(request, cancellationToken);
            if (form is null)
            {
                return Results.ValidationProblem(formErrors);
            }
            var (file, fileErrors) = await ReadExcelFileAsync(form, cancellationToken);
            var expectedFileSha256 = form["expectedFileSha256"].ToString().Trim();
            var operationValue = form["operationId"].ToString().Trim();
            var rows = ReadExcelRows(form, fileErrors);
            var confirmedDuplicateRowNumbers = ReadConfirmedDuplicateRowNumbers(form, fileErrors);
            if (!Regex.IsMatch(expectedFileSha256, "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant))
            {
                fileErrors["expectedFileSha256"] = ["미리보기에서 받은 파일 식별자를 입력해 주세요."];
            }
            if (!Guid.TryParse(operationValue, out var operationId) || operationId == Guid.Empty)
            {
                fileErrors["operationId"] = ["요청 식별자를 입력해 주세요."];
            }
            if (file is null || fileErrors.Count > 0)
            {
                return Results.ValidationProblem(fileErrors);
            }

            var result = await store.ApplyExcelAsync(
                file,
                expectedFileSha256,
                operationId,
                userId.Value,
                rows,
                confirmedDuplicateRowNumbers,
                cancellationToken);
            if (result is { Status: OsanProjectExcelApplyStatus.Success, Value: { Replayed: true } replayed })
            {
                foreach (var projectId in replayed.ProjectIds)
                {
                    var accessRecord = await store.GetAccessRecordAsync(projectId, cancellationToken);
                    if (accessRecord is null || !CanAccessProject(user, accessRecord.ProjectKey))
                    {
                        return Results.Forbid();
                    }
                }
            }

            return result.Status switch
            {
                OsanProjectExcelApplyStatus.Success when result.Value is not null => Results.Ok(result.Value),
                OsanProjectExcelApplyStatus.Validation => Results.ValidationProblem(
                    result.Errors ?? new Dictionary<string, string[]>()),
                OsanProjectExcelApplyStatus.FileChanged => Results.Conflict(new OsanProjectErrorResponse(
                    "osan_project_excel_file_changed",
                    "미리보기 후 파일이 변경되었습니다. 다시 미리보기해 주세요.")),
                OsanProjectExcelApplyStatus.ConfirmationRequired => Results.Conflict(
                    new OsanProjectExcelConfirmationRequiredResponse(
                        "osan_project_excel_confirmation_required",
                        "중복 가능성이 있는 행을 확인한 뒤 다시 등록해 주세요.",
                        result.ConfirmationRowNumbers ?? [])),
                OsanProjectExcelApplyStatus.ProjectCodeConflict => Results.Conflict(new OsanProjectErrorResponse(
                    "osan_project_excel_project_code_conflict",
                    "이미 등록된 프로젝트 코드가 있습니다. 다시 미리보기해 주세요.")),
                OsanProjectExcelApplyStatus.OperationConflict => Results.Conflict(new OsanProjectErrorResponse(
                    "osan_project_excel_operation_conflict",
                    "같은 요청 식별자가 다른 일괄 등록에 사용되었습니다.",
                    new Dictionary<string, string[]>
                    {
                        ["operationId"] = ["새 요청으로 다시 시도해 주세요."]
                    })),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
            };
        })
        .RequireAuthorization(QmsPolicies.ProjectCreate)
        .WithMetadata(new RequestSizeLimitAttribute(OsanProjectExcelParser.MaximumMultipartBytes))
        .WithName("ApplyOsanProjectImport");

        api.MapGet("", async (
            OsanProjectStore store,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!IsSelectedOsan(connectionStringProvider))
            {
                return BusinessUnitDenied();
            }

            if (!ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var projects = await store.ListAsync(
                ProjectEndpointExtensions.GetProjectAccessScope(user),
                cancellationToken);
            return Results.Ok(projects);
        })
        .RequireAuthorization()
        .WithName("ListOsanProjects");

        api.MapGet("/{projectId:guid}", async (
            Guid projectId,
            OsanProjectStore store,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!IsSelectedOsan(connectionStringProvider))
            {
                return BusinessUnitDenied();
            }

            if (!ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var accessRecord = await store.GetAccessRecordAsync(projectId, cancellationToken);
            if (accessRecord is null)
            {
                return Results.NotFound();
            }

            if (!CanAccessProject(user, accessRecord.ProjectKey))
            {
                return Results.Forbid();
            }

            var project = await store.GetAsync(projectId, cancellationToken);
            return project is null ? Results.NotFound() : Results.Ok(project);
        })
        .RequireAuthorization()
        .WithName("GetOsanProject");

        api.MapPost("", async (
            CreateOsanProjectRequest request,
            OsanProjectStore store,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!IsSelectedOsan(connectionStringProvider))
            {
                return BusinessUnitDenied();
            }

            if (!ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var userId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var (input, errors) = OsanProjectInputNormalizer.Normalize(request);
            if (input is null)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await store.CreateAsync(input, userId.Value, cancellationToken);
            if (result is
                {
                    Status: OsanProjectCreateStatus.Success,
                    Value: { Replayed: true } replayed
                })
            {
                var accessRecord = await store.GetAccessRecordAsync(
                    replayed.Project.ProjectId,
                    cancellationToken);
                if (accessRecord is null || !CanAccessProject(user, accessRecord.ProjectKey))
                {
                    return Results.Forbid();
                }
            }

            return result.Status switch
            {
                OsanProjectCreateStatus.Success when result.Value is not null =>
                    Results.Created($"/api/osan/projects/{result.Value.Project.ProjectId}", result.Value),
                OsanProjectCreateStatus.ProjectCodeConflict => Results.Conflict(new OsanProjectErrorResponse(
                    "osan_project_code_conflict",
                    "이미 등록된 프로젝트 코드입니다.",
                    new Dictionary<string, string[]>
                    {
                        [nameof(request.ProjectCode)] = ["이미 등록된 프로젝트 코드입니다."]
                    })),
                OsanProjectCreateStatus.OperationConflict => Results.Conflict(new OsanProjectErrorResponse(
                    "osan_project_operation_conflict",
                    "같은 요청 식별자가 다른 입력에 사용되었습니다.",
                    new Dictionary<string, string[]>
                    {
                        [nameof(request.OperationId)] = ["새 요청으로 다시 시도해 주세요."]
                    })),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
            };
        })
        .RequireAuthorization(QmsPolicies.ProjectCreate)
        .WithName("CreateOsanProject");

        return app;
    }

    internal static (OsanDashboardQuery? Query, Dictionary<string, string[]> Errors) ParseDashboardQuery(
        IQueryCollection values)
    {
        var errors = new Dictionary<string, string[]>();
        var search = values["search"].ToString().Trim();
        if (search.Length > 200)
        {
            errors["search"] = ["검색어는 200자 이하여야 합니다."];
        }

        var status = values["status"].ToString().Trim();
        if (status.Length == 0)
        {
            status = OsanDashboardStatuses.All;
        }
        else if (!OsanDashboardStatuses.IsValid(status))
        {
            errors["status"] = ["상태 필터가 올바르지 않습니다."];
        }

        var page = ParsePositiveInteger(values["page"].ToString(), 1, "page", errors);
        var pageSize = ParsePositiveInteger(values["pageSize"].ToString(), 10, "pageSize", errors);
        if (pageSize > 100)
        {
            errors["pageSize"] = ["페이지 크기는 100 이하여야 합니다."];
        }

        var view = values["view"].ToString().Trim();
        if (view.Length == 0)
        {
            view = OsanDashboardViews.Progress;
        }
        else if (!OsanDashboardViews.IsValid(view))
        {
            errors["view"] = ["대시보드 보기가 올바르지 않습니다."];
        }

        return errors.Count == 0
            ? (new OsanDashboardQuery(search, status, page, pageSize, view), errors)
            : (null, errors);
    }

    private static int ParsePositiveInteger(
        string raw,
        int defaultValue,
        string field,
        IDictionary<string, string[]> errors)
    {
        if (raw.Length == 0)
        {
            return defaultValue;
        }
        if (!int.TryParse(raw, out var value) || value < 1)
        {
            errors[field] = ["1 이상의 정수를 입력해 주세요."];
            return defaultValue;
        }
        return value;
    }

    private static bool IsSelectedOsan(DatabaseConnectionStringProvider connectionStringProvider) =>
        string.Equals(
            connectionStringProvider.GetCurrentBusinessUnit()?.Code,
            BusinessUnitCodes.Osan,
            StringComparison.Ordinal);

    internal static bool CanAccessProject(ClaimsPrincipal user, string projectKey) =>
        ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectReadAll)
        || user.FindAll(QmsClaimTypes.Project).Any(claim =>
            string.Equals(claim.Value, projectKey, StringComparison.Ordinal));

    private static IResult BusinessUnitDenied() => Results.Json(
        new BusinessUnitAccessDeniedResponse(
            "business_unit_capability_disabled",
            "선택한 사업부에서 사용할 수 없는 기능입니다."),
        statusCode: StatusCodes.Status403Forbidden);

    private static IResult? GuardImportAccess(
        DatabaseConnectionStringProvider connectionStringProvider,
        ClaimsPrincipal user)
    {
        if (!IsSelectedOsan(connectionStringProvider))
        {
            return BusinessUnitDenied();
        }
        return ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectRead)
            ? null
            : Results.Forbid();
    }

    private static async Task<(UploadedExcelFile? File, Dictionary<string, string[]> Errors)> ReadExcelFileAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var (form, errors) = await ReadExcelFormAsync(request, cancellationToken);
        return form is null
            ? (null, errors)
            : await ReadExcelFileAsync(form, cancellationToken);
    }

    private static IReadOnlyList<OsanProjectExcelRowRequest>? ReadExcelRows(
        IFormCollection form,
        IDictionary<string, string[]> errors)
    {
        var json = form["rows"].ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            var rows = JsonSerializer.Deserialize<IReadOnlyList<OsanProjectExcelRowRequest>>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 16 });
            if (rows is null)
            {
                errors["rows"] = ["행 입력을 확인해 주세요."];
            }
            return rows;
        }
        catch (JsonException)
        {
            errors["rows"] = ["행 입력 JSON을 확인해 주세요."];
            return null;
        }
    }

    private static IReadOnlySet<int> ReadConfirmedDuplicateRowNumbers(
        IFormCollection form,
        IDictionary<string, string[]> errors)
    {
        var json = form["confirmedDuplicateRowNumbers"].ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            return new HashSet<int>();
        }
        try
        {
            var rows = JsonSerializer.Deserialize<IReadOnlyList<int>>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 4 });
            if (rows is null || rows.Any(row => row < 1) || rows.Count != rows.Distinct().Count())
            {
                errors["confirmedDuplicateRowNumbers"] = ["중복 확인 행 번호를 확인해 주세요."];
                return new HashSet<int>();
            }
            return rows.ToHashSet();
        }
        catch (JsonException)
        {
            errors["confirmedDuplicateRowNumbers"] = ["중복 확인 행 번호 JSON을 확인해 주세요."];
            return new HashSet<int>();
        }
    }

    private static async Task<(IFormCollection? Form, Dictionary<string, string[]> Errors)> ReadExcelFormAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return (null, new Dictionary<string, string[]>
            {
                ["file"] = ["multipart/form-data 형식으로 Excel 파일을 업로드해 주세요."]
            });
        }
        try
        {
            return (await request.ReadFormAsync(cancellationToken), new Dictionary<string, string[]>());
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or BadHttpRequestException)
        {
            return (null, new Dictionary<string, string[]>
            {
                ["file"] = ["Excel 업로드 본문을 읽을 수 없습니다."]
            });
        }
    }

    private static async Task<(UploadedExcelFile? File, Dictionary<string, string[]> Errors)> ReadExcelFileAsync(
        IFormCollection form,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (form.Files.Count != 1
            || !string.Equals(form.Files[0].Name, "file", StringComparison.Ordinal))
        {
            errors["file"] = ["Excel 파일 1개를 선택해 주세요."];
            return (null, errors);
        }

        var file = form.Files[0];
        var metadataErrors = OsanProjectExcelParser.ValidateUploadMetadata(file);
        if (metadataErrors.Count > 0)
        {
            errors["file"] = metadataErrors.ToArray();
            return (null, errors);
        }
        try
        {
            return (await OsanProjectExcelParser.ReadUploadedFileAsync(file, cancellationToken), errors);
        }
        catch (InvalidDataException exception)
        {
            errors["file"] = [exception.Message];
            return (null, errors);
        }
        catch (IOException)
        {
            errors["file"] = ["Excel 파일을 읽을 수 없습니다."];
            return (null, errors);
        }
    }
}
