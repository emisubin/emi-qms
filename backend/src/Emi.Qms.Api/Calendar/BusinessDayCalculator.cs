namespace Emi.Qms.Api.Calendar;

public sealed class BusinessDayCalculator
{
    private readonly IReadOnlyDictionary<DateOnly, BusinessCalendarHoliday> holidaysByDate;

    public BusinessDayCalculator(IEnumerable<BusinessCalendarHoliday> holidays)
    {
        holidaysByDate = holidays
            .GroupBy(holiday => holiday.Date)
            .ToDictionary(
                group => group.Key,
                group => MergeHolidays(group),
                DateOnlyComparer.Instance);
    }

    public bool IsWeekend(DateOnly date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    public bool IsHoliday(DateOnly date)
    {
        return holidaysByDate.ContainsKey(date);
    }

    public bool IsCompanyHoliday(DateOnly date)
    {
        return holidaysByDate.TryGetValue(date, out var holiday)
            && string.Equals(holiday.HolidayType, SystemHolidayTypes.Company, StringComparison.Ordinal);
    }

    public bool IsBusinessDay(DateOnly date)
    {
        return !IsWeekend(date) && !IsHoliday(date);
    }

    public DateOnly AddBusinessDays(DateOnly date, int days)
    {
        if (days < 0)
        {
            return SubtractBusinessDays(date, Math.Abs(days));
        }

        var cursor = date;
        var remaining = days;
        while (remaining > 0)
        {
            cursor = cursor.AddDays(1);
            if (IsBusinessDay(cursor))
            {
                remaining -= 1;
            }
        }

        return cursor;
    }

    public DateOnly SubtractBusinessDays(DateOnly date, int days)
    {
        if (days < 0)
        {
            return AddBusinessDays(date, Math.Abs(days));
        }

        var cursor = date;
        var remaining = days;
        while (remaining > 0)
        {
            cursor = cursor.AddDays(-1);
            if (IsBusinessDay(cursor))
            {
                remaining -= 1;
            }
        }

        return cursor;
    }

    public DateOnly GetPreviousBusinessDay(DateOnly date)
    {
        return SubtractBusinessDays(date, 1);
    }

    public DateOnly GetNextBusinessDay(DateOnly date)
    {
        return AddBusinessDays(date, 1);
    }

    public int CountBusinessDays(DateOnly startInclusive, DateOnly endInclusive)
    {
        if (endInclusive < startInclusive)
        {
            return 0;
        }

        var count = 0;
        for (var cursor = startInclusive; cursor <= endInclusive; cursor = cursor.AddDays(1))
        {
            if (IsBusinessDay(cursor))
            {
                count += 1;
            }
        }

        return count;
    }

    public BusinessCalendarDayResponse Describe(DateOnly date)
    {
        holidaysByDate.TryGetValue(date, out var holiday);
        var isWeekend = IsWeekend(date);
        var isHoliday = holiday is not null;
        return new BusinessCalendarDayResponse(
            date,
            isWeekend,
            isHoliday,
            isHoliday && string.Equals(holiday!.HolidayType, SystemHolidayTypes.Company, StringComparison.Ordinal),
            !isWeekend && !isHoliday,
            holiday?.Name,
            holiday?.HolidayType);
    }

    private static BusinessCalendarHoliday MergeHolidays(IEnumerable<BusinessCalendarHoliday> holidays)
    {
        var ordered = holidays
            .OrderBy(holiday => HolidayTypePriority(holiday.HolidayType))
            .ThenBy(holiday => holiday.Name, StringComparer.Ordinal)
            .ToArray();
        var primary = ordered[0];
        var names = ordered
            .Select(holiday => holiday.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return primary with { Name = string.Join(" / ", names) };
    }

    private static int HolidayTypePriority(string holidayType)
    {
        return holidayType switch
        {
            SystemHolidayTypes.Company => 0,
            SystemHolidayTypes.Temporary => 1,
            SystemHolidayTypes.Substitute => 2,
            _ => 3
        };
    }

    private sealed class DateOnlyComparer : IEqualityComparer<DateOnly>
    {
        public static readonly DateOnlyComparer Instance = new();

        public bool Equals(DateOnly x, DateOnly y)
        {
            return x == y;
        }

        public int GetHashCode(DateOnly obj)
        {
            return obj.GetHashCode();
        }
    }
}
