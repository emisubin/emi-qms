namespace Emi.Qms.Api.Calendar;

public static class SystemHolidayTypes
{
    public const string National = "National";
    public const string Substitute = "Substitute";
    public const string Temporary = "Temporary";
    public const string Company = "Company";

    public static string Normalize(string? value)
    {
        return value?.Trim() switch
        {
            Company => Company,
            Substitute => Substitute,
            Temporary => Temporary,
            National => National,
            _ => National
        };
    }

    public static bool TryNormalize(string? value, out string holidayType)
    {
        holidayType = value?.Trim() switch
        {
            Company => Company,
            Substitute => Substitute,
            Temporary => Temporary,
            National => National,
            _ => ""
        };

        return !string.IsNullOrEmpty(holidayType);
    }
}

public sealed record BusinessCalendarResponse(
    DateOnly From,
    DateOnly To,
    string CountryCode,
    IReadOnlyList<BusinessCalendarDayResponse> Days);

public sealed record BusinessCalendarDayResponse(
    DateOnly Date,
    bool IsWeekend,
    bool IsHoliday,
    bool IsCompanyHoliday,
    bool IsBusinessDay,
    string? HolidayName,
    string? HolidayType);

public sealed record BusinessCalendarHoliday(
    DateOnly Date,
    string Name,
    string HolidayType);
