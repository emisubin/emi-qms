using Emi.Qms.Api.Calendar;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class BusinessDayCalculatorTests
{
    [Fact]
    public void IsBusinessDay_ReturnsFalseForWeekendAndHolidayTypes()
    {
        var calculator = CreateCalculator(
            new BusinessCalendarHoliday(new DateOnly(2026, 7, 3), "임시공휴일", SystemHolidayTypes.Temporary),
            new BusinessCalendarHoliday(new DateOnly(2026, 7, 6), "공식 대체공휴일", SystemHolidayTypes.Substitute),
            new BusinessCalendarHoliday(new DateOnly(2026, 7, 7), "회사 휴일", SystemHolidayTypes.Company),
            new BusinessCalendarHoliday(new DateOnly(2026, 7, 17), "제헌절", SystemHolidayTypes.National));

        Assert.True(calculator.IsBusinessDay(new DateOnly(2026, 7, 2)));
        Assert.False(calculator.IsBusinessDay(new DateOnly(2026, 7, 3)));
        Assert.False(calculator.IsBusinessDay(new DateOnly(2026, 7, 4)));
        Assert.False(calculator.IsBusinessDay(new DateOnly(2026, 7, 5)));
        Assert.False(calculator.IsBusinessDay(new DateOnly(2026, 7, 6)));
        Assert.False(calculator.IsBusinessDay(new DateOnly(2026, 7, 7)));
        Assert.False(calculator.IsBusinessDay(new DateOnly(2026, 7, 17)));
        Assert.True(calculator.IsCompanyHoliday(new DateOnly(2026, 7, 7)));
    }

    [Fact]
    public void BusinessDayNavigation_SkipsWeekendAndHolidays()
    {
        var calculator = CreateCalculator(
            new BusinessCalendarHoliday(new DateOnly(2026, 7, 3), "임시공휴일", SystemHolidayTypes.Temporary),
            new BusinessCalendarHoliday(new DateOnly(2026, 7, 6), "공식 대체공휴일", SystemHolidayTypes.Substitute));

        Assert.Equal(new DateOnly(2026, 7, 2), calculator.GetPreviousBusinessDay(new DateOnly(2026, 7, 7)));
        Assert.Equal(new DateOnly(2026, 7, 7), calculator.GetNextBusinessDay(new DateOnly(2026, 7, 2)));
        Assert.Equal(new DateOnly(2026, 7, 8), calculator.AddBusinessDays(new DateOnly(2026, 7, 2), 2));
        Assert.Equal(new DateOnly(2026, 7, 2), calculator.SubtractBusinessDays(new DateOnly(2026, 7, 8), 2));
    }

    [Fact]
    public void CountBusinessDays_UsesInclusiveDateOnlyRange()
    {
        var calculator = CreateCalculator(
            new BusinessCalendarHoliday(new DateOnly(2026, 12, 31), "회사 휴일", SystemHolidayTypes.Company),
            new BusinessCalendarHoliday(new DateOnly(2027, 1, 1), "신정", SystemHolidayTypes.National));

        Assert.Equal(2, calculator.CountBusinessDays(new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 4)));
    }

    [Fact]
    public void Describe_MergesDuplicateHolidaySourcesAndPrioritizesCompanyHoliday()
    {
        var calculator = CreateCalculator(
            new BusinessCalendarHoliday(new DateOnly(2026, 8, 14), "임시공휴일", SystemHolidayTypes.Temporary),
            new BusinessCalendarHoliday(new DateOnly(2026, 8, 14), "회사 여름휴가", SystemHolidayTypes.Company));

        var day = calculator.Describe(new DateOnly(2026, 8, 14));

        Assert.True(day.IsHoliday);
        Assert.True(day.IsCompanyHoliday);
        Assert.False(day.IsBusinessDay);
        Assert.Equal(SystemHolidayTypes.Company, day.HolidayType);
        Assert.Contains("회사 여름휴가", day.HolidayName, StringComparison.Ordinal);
        Assert.Contains("임시공휴일", day.HolidayName, StringComparison.Ordinal);
    }

    private static BusinessDayCalculator CreateCalculator(params BusinessCalendarHoliday[] holidays)
    {
        return new BusinessDayCalculator(holidays);
    }
}
