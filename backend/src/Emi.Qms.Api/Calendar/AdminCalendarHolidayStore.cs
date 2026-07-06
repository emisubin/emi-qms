using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Calendar;

public sealed class AdminCalendarHolidayStore(DatabaseConnectionStringProvider connectionStringProvider, TimeProvider timeProvider)
{
    private const string DefaultCountryCode = "KR";
    private const string AdminSource = "AdminManual";

    public async Task<AdminCalendarHolidayListResponse> ListAsync(int year, string? countryCode, CancellationToken cancellationToken)
    {
        var normalizedCountryCode = NormalizeCountryCode(countryCode);
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, holiday_date, name, country_code, holiday_type, is_active, note, source, created_at_utc, updated_at_utc
            from system_holidays
            where country_code = @country_code
              and holiday_date >= @date_from
              and holiday_date <= @date_to
            order by holiday_date, holiday_type, name;
            """);
        command.Parameters.AddWithValue("country_code", normalizedCountryCode);
        command.Parameters.AddWithValue("date_from", new DateOnly(year, 1, 1));
        command.Parameters.AddWithValue("date_to", new DateOnly(year, 12, 31));

        var holidays = new List<AdminCalendarHolidayResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            holidays.Add(ReadHoliday(reader));
        }

        return new AdminCalendarHolidayListResponse(year, normalizedCountryCode, holidays);
    }

    public async Task<bool> HasActiveDuplicateAsync(DateOnly date, string holidayType, Guid? exceptHolidayId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select exists (
                select 1
                from system_holidays
                where country_code = @country_code
                  and holiday_date = @holiday_date
                  and holiday_type = @holiday_type
                  and is_active = true
                  and (@except_id is null or id <> @except_id)
            );
            """);
        command.Parameters.AddWithValue("country_code", DefaultCountryCode);
        command.Parameters.AddWithValue("holiday_date", date);
        command.Parameters.AddWithValue("holiday_type", holidayType);
        command.Parameters.Add("except_id", NpgsqlDbType.Uuid).Value = exceptHolidayId ?? (object)DBNull.Value;
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<CalendarHolidayMutationResult> CreateAsync(
        DateOnly date,
        string name,
        string holidayType,
        bool isActive,
        string? note,
        CancellationToken cancellationToken)
    {
        if (isActive && await HasActiveDuplicateAsync(date, holidayType, null, cancellationToken))
        {
            return CalendarHolidayMutationResult.Failure("같은 날짜와 휴일유형의 활성 휴일이 이미 있습니다.");
        }

        var now = timeProvider.GetUtcNow();
        var holidayId = Guid.NewGuid();
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            insert into system_holidays (
                id, holiday_date, name, country_code, source, source_key, holiday_type,
                is_active, note, created_at_utc, updated_at_utc
            )
            values (
                @id, @holiday_date, @name, @country_code, @source, @source_key, @holiday_type,
                @is_active, @note, @created_at_utc, @updated_at_utc
            )
            returning id, holiday_date, name, country_code, holiday_type, is_active, note, source, created_at_utc, updated_at_utc;
            """);
        BindHolidayInsert(command, holidayId, date, name, holidayType, isActive, note, now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return CalendarHolidayMutationResult.Failure("휴일을 등록할 수 없습니다.");
        }

        return CalendarHolidayMutationResult.Success(ReadHoliday(reader));
    }

    public async Task<CalendarHolidayMutationResult> UpdateAsync(
        Guid holidayId,
        DateOnly date,
        string name,
        string holidayType,
        bool isActive,
        string? note,
        CancellationToken cancellationToken)
    {
        if (isActive && await HasActiveDuplicateAsync(date, holidayType, holidayId, cancellationToken))
        {
            return CalendarHolidayMutationResult.Failure("같은 날짜와 휴일유형의 활성 휴일이 이미 있습니다.");
        }

        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            update system_holidays
            set holiday_date = @holiday_date,
                name = @name,
                holiday_type = @holiday_type,
                is_active = @is_active,
                note = @note,
                updated_at_utc = @updated_at_utc
            where id = @id
            returning id, holiday_date, name, country_code, holiday_type, is_active, note, source, created_at_utc, updated_at_utc;
            """);
        command.Parameters.AddWithValue("id", holidayId);
        command.Parameters.AddWithValue("holiday_date", date);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("holiday_type", holidayType);
        command.Parameters.AddWithValue("is_active", isActive);
        command.Parameters.Add("note", NpgsqlDbType.Text).Value = TrimToNull(note) ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("updated_at_utc", now);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return CalendarHolidayMutationResult.Failure("휴일을 찾을 수 없습니다.");
        }

        return CalendarHolidayMutationResult.Success(ReadHoliday(reader));
    }

    public async Task<CalendarHolidayMutationResult> DeactivateAsync(Guid holidayId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            update system_holidays
            set is_active = false,
                updated_at_utc = @updated_at_utc
            where id = @id
            returning id, holiday_date, name, country_code, holiday_type, is_active, note, source, created_at_utc, updated_at_utc;
            """);
        command.Parameters.AddWithValue("id", holidayId);
        command.Parameters.AddWithValue("updated_at_utc", timeProvider.GetUtcNow());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return CalendarHolidayMutationResult.Failure("휴일을 찾을 수 없습니다.");
        }

        return CalendarHolidayMutationResult.Success(ReadHoliday(reader));
    }

    public async Task<IReadOnlyDictionary<string, AdminCalendarHolidayResponse>> GetExistingByDateTypeAsync(
        IEnumerable<ParsedCalendarHolidayExcelRow> parsedRows,
        CancellationToken cancellationToken)
    {
        var keys = parsedRows
            .Where(row => row.Date is not null && !string.IsNullOrWhiteSpace(row.HolidayType))
            .Select(row => Key(row.Date!.Value, row.HolidayType!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (keys.Length == 0)
        {
            return new Dictionary<string, AdminCalendarHolidayResponse>(StringComparer.Ordinal);
        }

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, holiday_date, name, country_code, holiday_type, is_active, note, source, created_at_utc, updated_at_utc
            from system_holidays
            where country_code = @country_code
              and concat(to_char(holiday_date, 'YYYY-MM-DD'), '|', holiday_type) = any(@keys)
            order by is_active desc, updated_at_utc desc;
            """);
        command.Parameters.AddWithValue("country_code", DefaultCountryCode);
        command.Parameters.AddWithValue("keys", keys);

        var holidays = new Dictionary<string, AdminCalendarHolidayResponse>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var holiday = ReadHoliday(reader);
            holidays.TryAdd(Key(holiday.Date, holiday.HolidayType), holiday);
        }

        return holidays;
    }

    public async Task<CalendarHolidayExcelApplyResponse> ApplyExcelRowsAsync(
        IEnumerable<CalendarHolidayExcelPreviewRowResponse> rows,
        CancellationToken cancellationToken)
    {
        var saveableRows = rows
            .Where(row => row.ErrorMessages.Count == 0 && row.Date is not null && !string.IsNullOrWhiteSpace(row.Name) && !string.IsNullOrWhiteSpace(row.HolidayType))
            .ToArray();
        if (saveableRows.Length == 0)
        {
            return new CalendarHolidayExcelApplyResponse(0, 0, rows.Count(), []);
        }

        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var inserted = 0;
        var updated = 0;
        var ids = new List<Guid>();
        foreach (var row in saveableRows)
        {
            var existingId = row.ExistingHolidayId ?? await FindHolidayIdByDateTypeAsync(connection, transaction, row.Date!.Value, row.HolidayType!, cancellationToken);
            if (existingId is null)
            {
                var id = Guid.NewGuid();
                await using var insert = new NpgsqlCommand("""
                    insert into system_holidays (
                        id, holiday_date, name, country_code, source, source_key, holiday_type,
                        is_active, note, created_at_utc, updated_at_utc
                    )
                    values (
                        @id, @holiday_date, @name, @country_code, @source, @source_key, @holiday_type,
                        true, @note, @created_at_utc, @updated_at_utc
                    );
                    """, connection, transaction);
                BindHolidayInsert(insert, id, row.Date!.Value, row.Name!, row.HolidayType!, true, row.Note, now);
                await insert.ExecuteNonQueryAsync(cancellationToken);
                ids.Add(id);
                inserted += 1;
            }
            else
            {
                await using var update = new NpgsqlCommand("""
                    update system_holidays
                    set name = @name,
                        is_active = true,
                        note = @note,
                        updated_at_utc = @updated_at_utc
                    where id = @id;
                    """, connection, transaction);
                update.Parameters.AddWithValue("id", existingId.Value);
                update.Parameters.AddWithValue("name", row.Name!);
                update.Parameters.Add("note", NpgsqlDbType.Text).Value = TrimToNull(row.Note) ?? (object)DBNull.Value;
                update.Parameters.AddWithValue("updated_at_utc", now);
                await update.ExecuteNonQueryAsync(cancellationToken);
                ids.Add(existingId.Value);
                updated += 1;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new CalendarHolidayExcelApplyResponse(inserted, updated, rows.Count() - saveableRows.Length, ids);
    }

    private static async Task<Guid?> FindHolidayIdByDateTypeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateOnly date,
        string holidayType,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select id
            from system_holidays
            where country_code = @country_code
              and holiday_date = @holiday_date
              and holiday_type = @holiday_type
            order by is_active desc, updated_at_utc desc
            limit 1;
            """, connection, transaction);
        command.Parameters.AddWithValue("country_code", DefaultCountryCode);
        command.Parameters.AddWithValue("holiday_date", date);
        command.Parameters.AddWithValue("holiday_type", holidayType);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private static void BindHolidayInsert(
        NpgsqlCommand command,
        Guid holidayId,
        DateOnly date,
        string name,
        string holidayType,
        bool isActive,
        string? note,
        DateTimeOffset now)
    {
        command.Parameters.AddWithValue("id", holidayId);
        command.Parameters.AddWithValue("holiday_date", date);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("country_code", DefaultCountryCode);
        command.Parameters.AddWithValue("source", AdminSource);
        command.Parameters.AddWithValue("source_key", $"Admin:{date:yyyyMMdd}:{holidayType}:{holidayId:N}");
        command.Parameters.AddWithValue("holiday_type", holidayType);
        command.Parameters.AddWithValue("is_active", isActive);
        command.Parameters.Add("note", NpgsqlDbType.Text).Value = TrimToNull(note) ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("created_at_utc", now);
        command.Parameters.AddWithValue("updated_at_utc", now);
    }

    private static AdminCalendarHolidayResponse ReadHoliday(NpgsqlDataReader reader)
    {
        return new AdminCalendarHolidayResponse(
            reader.GetGuid(0),
            reader.GetFieldValue<DateOnly>(1),
            reader.GetString(2),
            reader.GetString(3),
            SystemHolidayTypes.Normalize(reader.GetString(4)),
            reader.GetBoolean(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetString(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetFieldValue<DateTimeOffset>(9));
    }

    private static string Key(DateOnly date, string holidayType)
    {
        return $"{date:yyyy-MM-dd}|{holidayType}";
    }

    private static string NormalizeCountryCode(string? countryCode)
    {
        return string.IsNullOrWhiteSpace(countryCode) ? DefaultCountryCode : countryCode.Trim().ToUpperInvariant();
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string is not configured.");
        return NpgsqlDataSource.Create(connectionString);
    }
}
