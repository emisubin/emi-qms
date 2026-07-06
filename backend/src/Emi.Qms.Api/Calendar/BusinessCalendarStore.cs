using Npgsql;

namespace Emi.Qms.Api.Calendar;

public sealed class BusinessCalendarStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<BusinessCalendarResponse> GetCalendarAsync(
        string? countryCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var normalizedCountryCode = NormalizeCountryCode(countryCode);
        var holidays = await ReadHolidaysAsync(normalizedCountryCode, from, to, cancellationToken);
        var calculator = new BusinessDayCalculator(holidays);
        var days = new List<BusinessCalendarDayResponse>();

        for (var cursor = from; cursor <= to; cursor = cursor.AddDays(1))
        {
            days.Add(calculator.Describe(cursor));
        }

        return new BusinessCalendarResponse(from, to, normalizedCountryCode, days);
    }

    private async Task<IReadOnlyList<BusinessCalendarHoliday>> ReadHolidaysAsync(
        string countryCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select holiday_date, name, holiday_type
            from system_holidays
            where is_active = true
              and country_code = @country_code
              and holiday_date >= @date_from
              and holiday_date <= @date_to
            order by holiday_date, name;
            """);
        command.Parameters.AddWithValue("country_code", countryCode);
        command.Parameters.AddWithValue("date_from", from);
        command.Parameters.AddWithValue("date_to", to);

        var holidays = new List<BusinessCalendarHoliday>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            holidays.Add(new BusinessCalendarHoliday(
                reader.GetFieldValue<DateOnly>(0),
                reader.GetString(1),
                SystemHolidayTypes.Normalize(reader.GetString(2))));
        }

        return holidays;
    }

    private static string NormalizeCountryCode(string? countryCode)
    {
        return string.IsNullOrWhiteSpace(countryCode) ? "KR" : countryCode.Trim().ToUpperInvariant();
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string is not configured.");
        return NpgsqlDataSource.Create(connectionString);
    }
}
