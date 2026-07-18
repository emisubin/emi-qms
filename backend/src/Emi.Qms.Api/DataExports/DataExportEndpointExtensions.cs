using System.Security.Claims;
using System.Text.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Procurement;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.DataExports;

public static class DataExportEndpointExtensions
{
    private static readonly IReadOnlySet<string> ProjectStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "All",
        "Active",
        "OnHold",
        "Completed",
        "Cancelled"
    };

    private static readonly IReadOnlySet<string> MyWorkStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "All",
        "Requested",
        "InProgress",
        "Completed"
    };

    public static IEndpointRouteBuilder MapDataExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/export", async (
            HttpRequest request,
            HttpContext httpContext,
            ExcelExportService exportService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var actorUserId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (actorUserId is null)
            {
                return Results.Unauthorized();
            }

            var dateRange = ProjectEndpointExtensions.ParseDateRange(request, "deliveryDateFrom", "deliveryDateTo");
            if (dateRange.Errors.Count > 0)
            {
                return Results.ValidationProblem(
                    dateRange.Errors,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "조회 조건을 확인해 주세요.");
            }

            var query = ProjectEndpointExtensions.ParseProjectListQuery(request);
            var status = string.IsNullOrWhiteSpace(query.Status) ? "All" : query.Status.Trim();
            if (!ProjectStatuses.Contains(status))
            {
                return UnsupportedFilter("status");
            }

            var filtersApplied = !string.IsNullOrWhiteSpace(query.Search)
                || status != "All"
                || query.DeliveryDateFrom is not null
                || query.DeliveryDateTo is not null;
            var filterSummary = $"상태 {StatusLabel(status)} · 검색 {FilterValue(query.Search)} · 납기 {DateRangeLabel(query.DeliveryDateFrom, query.DeliveryDateTo)}";
            var result = await exportService.ExportProjectsAsync(
                actorUserId.Value,
                query,
                ProjectEndpointExtensions.GetProjectAccessScope(user),
                ProjectEndpointExtensions.CanReadSalesAmount(user),
                filtersApplied,
                filterSummary,
                cancellationToken);
            return ToResult(result, httpContext);
        })
        .RequireAuthorization()
        .WithName("ExportProjects");

        app.MapPost("/api/projects/export/selected", async (
            HttpRequest request,
            HttpContext httpContext,
            ExcelExportService exportService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var actorUserId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (actorUserId is null)
            {
                return Results.Unauthorized();
            }

            var parsed = await ParseSelectedProjectIdsAsync(request, cancellationToken);
            if (parsed.Error is not null)
            {
                return parsed.Error;
            }

            var result = await exportService.ExportSelectedProjectsAsync(
                actorUserId.Value,
                parsed.ProjectIds!,
                ProjectEndpointExtensions.GetProjectAccessScope(user),
                ProjectEndpointExtensions.CanReadSalesAmount(user),
                cancellationToken);
            return ToResult(result, httpContext);
        })
        .RequireAuthorization()
        .WithName("ExportSelectedProjects");

        app.MapGet("/api/procurement/dashboard/export", async (
            HttpRequest request,
            HttpContext httpContext,
            string? search,
            ExcelExportService exportService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            var actorUserId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (actorUserId is null)
            {
                return Results.Unauthorized();
            }

            var dateRange = ProcurementEndpointExtensions.ParseDateRange(
                request,
                "expectedReceiptDateFrom",
                "expectedReceiptDateTo");
            if (dateRange.Errors.Count > 0)
            {
                return Results.ValidationProblem(
                    dateRange.Errors,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "조회 조건을 확인해 주세요.");
            }

            var filtersApplied = !string.IsNullOrWhiteSpace(search)
                || dateRange.From is not null
                || dateRange.To is not null;
            var filterSummary = $"검색 {FilterValue(search)} · 입고예정 {DateRangeLabel(dateRange.From, dateRange.To)}";
            var result = await exportService.ExportProcurementDashboardAsync(
                actorUserId.Value,
                search,
                dateRange.From,
                dateRange.To,
                ProjectEndpointExtensions.GetProjectAccessScope(user),
                filtersApplied,
                filterSummary,
                cancellationToken);
            return ToResult(result, httpContext);
        })
        .RequireAuthorization()
        .WithName("ExportProcurementDashboard");

        app.MapGet("/api/my-work/export", async (
            HttpContext httpContext,
            string? status,
            ExcelExportService exportService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var actorUserId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (actorUserId is null)
            {
                return Results.Unauthorized();
            }

            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim();
            if (!MyWorkStatuses.Contains(normalizedStatus))
            {
                return UnsupportedFilter("status");
            }

            var storeStatus = normalizedStatus == "All" ? null : normalizedStatus;
            var result = await exportService.ExportMyWorkAsync(
                actorUserId.Value,
                storeStatus,
                storeStatus is not null,
                $"상태 {StatusLabel(normalizedStatus)}",
                cancellationToken);
            return ToResult(result, httpContext);
        })
        .RequireAuthorization()
        .WithName("ExportMyWork");

        app.MapPost("/api/data-exports/selected", async (
            SelectedExportRequest request,
            HttpContext httpContext,
            SelectedExcelExportService exportService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var actorUserId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (actorUserId is null)
            {
                return Results.Unauthorized();
            }

            var screen = request.Screen?.Trim();
            if (screen is null || !SelectedExportScreens.All.Contains(screen))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["screen"] = ["지원하지 않는 내보내기 화면입니다."] },
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "선택 내보내기 요청을 확인해 주세요.");
            }

            if (!CanExportSelectedScreen(user, screen))
            {
                return Results.Forbid();
            }

            var validation = ValidateSelectedIds(request.Ids);
            if (validation.Error is not null)
            {
                return validation.Error;
            }

            var result = await exportService.ExportAsync(
                screen,
                validation.Ids!,
                request.Filters ?? new Dictionary<string, string?>(),
                actorUserId.Value,
                user,
                cancellationToken);
            return ToResult(result, httpContext);
        })
        .RequireAuthorization()
        .WithName("ExportSelectedRows");

        return app;
    }

    private static bool CanExportSelectedScreen(ClaimsPrincipal user, string screen)
    {
        if (SelectedExportScreens.RequiresAdminUsersRead(screen))
        {
            return ProjectEndpointExtensions.HasPermission(user, QmsPermissions.UsersManage);
        }

        if (SelectedExportScreens.RequiresAdminHistoryRead(screen))
        {
            return ProjectEndpointExtensions.HasPermission(user, QmsPermissions.AdminHistoryRead);
        }

        return screen switch
        {
            SelectedExportScreens.MyWork or SelectedExportScreens.Notifications => true,
            SelectedExportScreens.MaterialReceipts => ProjectEndpointExtensions.HasPermission(user, QmsPermissions.MaterialReceiptUpdate),
            SelectedExportScreens.MaterialIqc => ProjectEndpointExtensions.HasPermission(user, QmsPermissions.QualityInspect),
            SelectedExportScreens.Pending => ProjectEndpointExtensions.HasPermission(user, QmsPermissions.PendingRead),
            _ => ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ProjectRead)
        };
    }

    private static SelectedIdsValidationResult ValidateSelectedIds(IReadOnlyList<Guid>? ids)
    {
        if (ids is null || ids.Count == 0)
        {
            return InvalidSelectedIds("내보낼 항목을 한 건 이상 선택해 주세요.");
        }

        if (ids.Count > SelectedExcelExportService.MaximumSelectedRows)
        {
            return InvalidSelectedIds($"한 번에 최대 {SelectedExcelExportService.MaximumSelectedRows:N0}건까지 선택할 수 있습니다.");
        }

        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
        {
            return InvalidSelectedIds("중복되지 않은 올바른 항목을 선택해 주세요.");
        }

        return new SelectedIdsValidationResult(ids, null);
    }

    private static SelectedIdsValidationResult InvalidSelectedIds(string message)
    {
        return new SelectedIdsValidationResult(
            null,
            Results.ValidationProblem(
                new Dictionary<string, string[]> { ["ids"] = [message] },
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "선택 항목을 확인해 주세요."));
    }

    private static IResult ToResult(ExcelExportResult result, HttpContext httpContext)
    {
        if (result.Status == ExcelExportStatus.TooManyRows)
        {
            return Results.Problem(
                title: "내보낼 데이터가 10,000건을 초과합니다.",
                detail: "조회 조건을 좁혀 다시 시도해 주세요.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        if (result.Status == ExcelExportStatus.Busy)
        {
            return Results.Problem(
                title: "Excel 파일을 생성할 수 없습니다.",
                detail: "다른 파일을 생성 중입니다. 잠시 후 다시 시도해 주세요.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (result.Status == ExcelExportStatus.SelectionUnavailable)
        {
            return Results.Problem(
                title: "선택한 항목 중 내보낼 수 없는 항목이 있습니다.",
                detail: "목록을 새로고침한 뒤 다시 선택해 주세요.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        if (result.File is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        httpContext.Response.Headers["X-Export-Row-Count"] = result.File.RowCount.ToString();
        return Results.File(
            result.File.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.File.FileName);
    }

    private static IResult UnsupportedFilter(string key)
    {
        return Results.ValidationProblem(
            new Dictionary<string, string[]> { [key] = ["지원하지 않는 필터 값입니다."] },
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "조회 조건을 확인해 주세요.");
    }

    private static async Task<SelectedProjectIdsParseResult> ParseSelectedProjectIdsAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("projectIds", out var projectIdsElement)
                || projectIdsElement.ValueKind != JsonValueKind.Array)
            {
                return InvalidSelectedProjectIds("프로젝트 ID 목록을 배열로 입력해 주세요.");
            }

            var projectIds = new List<Guid>();
            var uniqueProjectIds = new HashSet<Guid>();
            foreach (var element in projectIdsElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String
                    || !Guid.TryParse(element.GetString()?.Trim(), out var projectId))
                {
                    return InvalidSelectedProjectIds("올바른 프로젝트 ID를 입력해 주세요.");
                }

                if (!uniqueProjectIds.Add(projectId))
                {
                    return InvalidSelectedProjectIds("중복되지 않은 프로젝트를 선택해 주세요.");
                }

                projectIds.Add(projectId);
                if (projectIds.Count > SelectedExcelExportService.MaximumSelectedRows)
                {
                    return InvalidSelectedProjectIds($"프로젝트는 한 번에 최대 {SelectedExcelExportService.MaximumSelectedRows:N0}건까지 선택할 수 있습니다.");
                }
            }

            return projectIds.Count == 0
                ? InvalidSelectedProjectIds("내보낼 프로젝트를 한 건 이상 선택해 주세요.")
                : new SelectedProjectIdsParseResult(projectIds, null);
        }
        catch (JsonException)
        {
            return InvalidSelectedProjectIds("프로젝트 ID 목록을 확인해 주세요.");
        }
    }

    private static SelectedProjectIdsParseResult InvalidSelectedProjectIds(string message)
    {
        return new SelectedProjectIdsParseResult(
            null,
            Results.ValidationProblem(
                new Dictionary<string, string[]> { ["projectIds"] = [message] },
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "선택 프로젝트를 확인해 주세요."));
    }

    private static string FilterValue(string? value)
    {
        return ExcelWorkbookBuilder.BoundText(string.IsNullOrWhiteSpace(value) ? "전체" : value.Trim(), 80);
    }

    private static string DateRangeLabel(DateOnly? from, DateOnly? to)
    {
        return from is null && to is null
            ? "전체"
            : $"{from?.ToString("yyyy-MM-dd") ?? "처음"}~{to?.ToString("yyyy-MM-dd") ?? "끝"}";
    }

    private static string StatusLabel(string status)
    {
        return status switch
        {
            "All" => "전체",
            "Active" => "진행 중",
            "OnHold" => "보류",
            "Completed" => "완료",
            "Cancelled" => "취소",
            "Requested" => "시작 전",
            "InProgress" => "진행 중",
            _ => status
        };
    }

    private sealed record SelectedProjectIdsParseResult(IReadOnlyList<Guid>? ProjectIds, IResult? Error);

    private sealed record SelectedIdsValidationResult(IReadOnlyList<Guid>? Ids, IResult? Error);
}

public sealed record SelectedExportRequest(
    string? Screen,
    IReadOnlyList<Guid>? Ids,
    IReadOnlyDictionary<string, string?>? Filters);
