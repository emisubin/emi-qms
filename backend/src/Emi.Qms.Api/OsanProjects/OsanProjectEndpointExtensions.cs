using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

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
}
