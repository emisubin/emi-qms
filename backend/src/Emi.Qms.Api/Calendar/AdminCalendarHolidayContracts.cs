namespace Emi.Qms.Api.Calendar;

public sealed record AdminCalendarHolidayListResponse(
    int Year,
    string CountryCode,
    IReadOnlyList<AdminCalendarHolidayResponse> Holidays);

public sealed record AdminCalendarHolidayResponse(
    Guid HolidayId,
    DateOnly Date,
    string Name,
    string CountryCode,
    string HolidayType,
    bool IsActive,
    string? Note,
    string Source,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpsertAdminCalendarHolidayRequest(
    DateOnly? Date,
    string? Name,
    string? HolidayType,
    bool? IsActive,
    string? Note);

public sealed record CalendarHolidayExcelPreviewResponse(
    string FileSha256,
    int TotalRows,
    int SaveableCount,
    int InsertCount,
    int UpdateCount,
    int ErrorCount,
    IReadOnlyList<CalendarHolidayExcelPreviewRowResponse> Rows);

public sealed record CalendarHolidayExcelPreviewRowResponse(
    int ExcelRowNumber,
    DateOnly? Date,
    string? Name,
    string? HolidayType,
    string? Note,
    string ResultType,
    Guid? ExistingHolidayId,
    IReadOnlyList<string> ErrorMessages);

public sealed record CalendarHolidayExcelApplyResponse(
    int InsertedCount,
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<Guid> HolidayIds);

public sealed record CalendarHolidayMutationResult(
    bool Succeeded,
    AdminCalendarHolidayResponse? Holiday,
    string? ErrorMessage)
{
    public static CalendarHolidayMutationResult Success(AdminCalendarHolidayResponse holiday) => new(true, holiday, null);
    public static CalendarHolidayMutationResult Failure(string message) => new(false, null, message);
}
