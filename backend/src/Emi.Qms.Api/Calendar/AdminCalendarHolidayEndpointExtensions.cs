using System.Security.Claims;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.PanelInformation;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.Calendar;

public static class AdminCalendarHolidayEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminCalendarHolidayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/calendar/holidays", async (
            int? year,
            AdminCalendarHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            var requestedYear = year ?? DateTimeOffset.UtcNow.Year;
            if (requestedYear is < 1900 or > 2200)
            {
                return Results.BadRequest(new { message = "조회 연도는 1900년부터 2200년 사이여야 합니다." });
            }

            return Results.Ok(await store.ListAsync(requestedYear, "KR", cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ListAdminCalendarHolidays");

        app.MapPost("/api/admin/calendar/holidays", async (
            UpsertAdminCalendarHolidayRequest request,
            AdminCalendarHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateRequest(request);
            if (validation.Count > 0)
            {
                return Results.ValidationProblem(validation);
            }

            var result = await store.CreateAsync(
                request.Date!.Value,
                request.Name!.Trim(),
                SystemHolidayTypes.Normalize(request.HolidayType),
                request.IsActive ?? true,
                request.Note,
                cancellationToken);
            return result.Succeeded && result.Holiday is not null
                ? Results.Created($"/api/admin/calendar/holidays/{result.Holiday.HolidayId}", result.Holiday)
                : Results.BadRequest(new { message = result.ErrorMessage ?? "휴일을 등록할 수 없습니다." });
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("CreateAdminCalendarHoliday");

        app.MapPut("/api/admin/calendar/holidays/{holidayId:guid}", async (
            Guid holidayId,
            UpsertAdminCalendarHolidayRequest request,
            AdminCalendarHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateRequest(request);
            if (validation.Count > 0)
            {
                return Results.ValidationProblem(validation);
            }

            var result = await store.UpdateAsync(
                holidayId,
                request.Date!.Value,
                request.Name!.Trim(),
                SystemHolidayTypes.Normalize(request.HolidayType),
                request.IsActive ?? true,
                request.Note,
                cancellationToken);
            return result.Succeeded && result.Holiday is not null
                ? Results.Ok(result.Holiday)
                : Results.BadRequest(new { message = result.ErrorMessage ?? "휴일을 저장할 수 없습니다." });
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("UpdateAdminCalendarHoliday");

        app.MapDelete("/api/admin/calendar/holidays/{holidayId:guid}", async (
            Guid holidayId,
            ClaimsPrincipal user,
            AdminCalendarHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.DeactivateAsync(holidayId, currentUserId.Value, cancellationToken);
            return result.Succeeded && result.Holiday is not null
                ? Results.Ok(result.Holiday)
                : Results.BadRequest(new { message = result.ErrorMessage ?? "휴일을 비활성화할 수 없습니다." });
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("DeactivateAdminCalendarHoliday");

        app.MapPost("/api/admin/calendar/holidays/{holidayId:guid}/restore", async (
            Guid holidayId,
            ClaimsPrincipal user,
            AdminCalendarHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.RestoreAsync(holidayId, currentUserId.Value, cancellationToken);
            return result.Succeeded && result.Holiday is not null
                ? Results.Ok(result.Holiday)
                : Results.BadRequest(new { message = result.ErrorMessage ?? "휴일을 복구할 수 없습니다." });
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("RestoreAdminCalendarHoliday");

        app.MapDelete("/api/admin/calendar/holidays/{holidayId:guid}/purge", async (
            Guid holidayId,
            ClaimsPrincipal user,
            AdminScheduledDeletionService deletionService,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await deletionService.PurgeHolidayNowAsync(holidayId, currentUserId.Value, cancellationToken);
            return result.Status == "Failed"
                ? Results.BadRequest(new { message = result.Message })
                : Results.Ok(ToSingleBulkActionResponse(holidayId, result));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("PurgeAdminCalendarHoliday");

        app.MapPost("/api/admin/calendar/holidays/bulk-delete", async (
            AdminBulkActionRequest request,
            ClaimsPrincipal user,
            AdminCalendarHolidayStore store,
            AdminScheduledDeletionService deletionService,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await store.BulkDeleteAsync(request.Ids, currentUserId.Value, deletionService, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("BulkDeleteAdminCalendarHolidays");

        app.MapPost("/api/admin/calendar/holidays/bulk-restore", async (
            AdminBulkActionRequest request,
            ClaimsPrincipal user,
            AdminCalendarHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await store.BulkRestoreAsync(request.Ids, currentUserId.Value, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("BulkRestoreAdminCalendarHolidays");

        app.MapGet("/api/admin/calendar/holidays/template", (
            CalendarHolidayExcelParser parser) =>
        {
            var content = parser.CreateTemplate();
            return Results.File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Calendar_Holidays_Template.xlsx");
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("DownloadAdminCalendarHolidayTemplate");

        app.MapPost("/api/admin/calendar/holidays/preview", async (
            HttpRequest request,
            CalendarHolidayExcelParser parser,
            AdminCalendarHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            var upload = await ReadHolidayExcelUploadAsync(request, cancellationToken);
            if (upload.Errors.Count > 0 || upload.File is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["File"] = upload.Errors.ToArray() });
            }

            var parsed = await parser.ParseAsync(upload.File, cancellationToken);
            if (parsed.FileErrors.Count > 0)
            {
                return Results.Ok(new CalendarHolidayExcelPreviewResponse(upload.File.FileSha256, parsed.TotalRows, 0, 0, 0, parsed.TotalRows, [
                    new CalendarHolidayExcelPreviewRowResponse(0, null, null, null, null, "Error", null, parsed.FileErrors)
                ]));
            }

            return Results.Ok(await BuildPreviewResponseAsync(parsed, store, cancellationToken));
        })
        .WithMetadata(new RequestSizeLimitAttribute(CalendarHolidayExcelParser.MaxExcelMultipartRequestBytes))
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("PreviewAdminCalendarHolidayExcel");

        app.MapPost("/api/admin/calendar/holidays/apply", async (
            HttpRequest request,
            CalendarHolidayExcelParser parser,
            AdminCalendarHolidayStore store,
            CancellationToken cancellationToken) =>
        {
            var upload = await ReadHolidayExcelUploadAsync(request, cancellationToken);
            if (upload.Errors.Count > 0 || upload.File is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["File"] = upload.Errors.ToArray() });
            }

            var parsed = await parser.ParseAsync(upload.File, cancellationToken);
            if (parsed.FileErrors.Count > 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["File"] = parsed.FileErrors.ToArray() });
            }

            var preview = await BuildPreviewResponseAsync(parsed, store, cancellationToken);
            return Results.Ok(await store.ApplyExcelRowsAsync(preview.Rows, cancellationToken));
        })
        .WithMetadata(new RequestSizeLimitAttribute(CalendarHolidayExcelParser.MaxExcelMultipartRequestBytes))
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ApplyAdminCalendarHolidayExcel");

        return app;
    }

    private static async Task<CalendarHolidayExcelPreviewResponse> BuildPreviewResponseAsync(
        ParsedCalendarHolidayExcelFile parsed,
        AdminCalendarHolidayStore store,
        CancellationToken cancellationToken)
    {
        var existing = await store.GetExistingByDateTypeAsync(parsed.Rows, cancellationToken);
        var rows = parsed.Rows.Select(row =>
        {
            AdminCalendarHolidayResponse? existingHoliday = null;
            if (row.Date is not null && !string.IsNullOrWhiteSpace(row.HolidayType))
            {
                existing.TryGetValue($"{row.Date:yyyy-MM-dd}|{row.HolidayType}", out existingHoliday);
            }

            var resultType = row.ErrorMessages.Count > 0
                ? "Error"
                : existingHoliday is null ? "Insert" : "Update";
            return new CalendarHolidayExcelPreviewRowResponse(
                row.ExcelRowNumber,
                row.Date,
                row.Name,
                row.HolidayType,
                row.Note,
                resultType,
                existingHoliday?.HolidayId,
                row.ErrorMessages);
        }).ToArray();

        return new CalendarHolidayExcelPreviewResponse(
            parsed.FileSha256,
            parsed.TotalRows,
            rows.Count(row => row.ErrorMessages.Count == 0),
            rows.Count(row => row.ResultType == "Insert"),
            rows.Count(row => row.ResultType == "Update"),
            rows.Count(row => row.ErrorMessages.Count > 0),
            rows);
    }

    private static Dictionary<string, string[]> ValidateRequest(UpsertAdminCalendarHolidayRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Date is null)
        {
            errors["date"] = ["날짜는 필수입니다."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["휴일명은 필수입니다."];
        }

        if (!SystemHolidayTypes.TryNormalize(request.HolidayType, out _))
        {
            errors["holidayType"] = ["휴일유형은 National, Substitute, Temporary, Company 중 하나여야 합니다."];
        }

        return errors;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        return Guid.TryParse(user.FindFirstValue(QmsClaimTypes.UserId), out var userId) ? userId : null;
    }

    private static AdminBulkActionResponse ToSingleBulkActionResponse(Guid id, AdminPurgeActionResult result)
    {
        return new AdminBulkActionResponse(
            1,
            result.Status == "Failed" ? 0 : 1,
            result.Status == "Failed" ? 1 : 0,
            0,
            [new AdminBulkActionItemResponse(id, result.Status, result.Message)]);
    }

    private static async Task<HolidayExcelUploadForm> ReadHolidayExcelUploadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return new HolidayExcelUploadForm(null, ["multipart/form-data 요청이 필요합니다."]);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return new HolidayExcelUploadForm(null, ["file 필드가 필요합니다."]);
        }

        var validation = CalendarHolidayExcelParser.ValidateUploadMetadata(file);
        if (validation.Count > 0)
        {
            return new HolidayExcelUploadForm(null, validation.ToArray());
        }

        return new HolidayExcelUploadForm(
            await CalendarHolidayExcelParser.ReadUploadedFileAsync(file, cancellationToken),
            []);
    }

    private sealed record HolidayExcelUploadForm(
        UploadedExcelFile? File,
        IReadOnlyList<string> Errors);
}
