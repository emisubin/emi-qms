using System.Globalization;
using System.Xml.Linq;
using Emi.Qms.Api.Calendar;
using Npgsql;

namespace Emi.Qms.Api.ProductionPlanning;

public sealed class SystemHolidayStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    IKoreanHolidayProvider koreanHolidayProvider,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SystemHolidayResponse>> ListAsync(
        string? countryCode,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select holiday_date, name, country_code, source, holiday_type
            from system_holidays
            where is_active = true
              and country_code = @country_code
              and (@date_from is null or holiday_date >= @date_from)
              and (@date_to is null or holiday_date <= @date_to)
            order by holiday_date, name;
            """);
        command.Parameters.AddWithValue("country_code", NormalizeCountryCode(countryCode));
        command.Parameters.AddWithValue("date_from", (object?)dateFrom ?? DBNull.Value);
        command.Parameters.AddWithValue("date_to", (object?)dateTo ?? DBNull.Value);

        var holidays = new List<SystemHolidayResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            holidays.Add(new SystemHolidayResponse(
                reader.GetFieldValue<DateOnly>(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                SystemHolidayTypes.Normalize(reader.GetString(4))));
        }

        return holidays;
    }

    public async Task<SyncKoreanHolidaysResponse> SyncKoreanHolidaysAsync(int? year, CancellationToken cancellationToken)
    {
        var targetYear = year ?? timeProvider.GetUtcNow().Year;
        if (targetYear is < 1900 or > 2200)
        {
            return new SyncKoreanHolidaysResponse(
                targetYear,
                "KR",
                0,
                false,
                "동기화 연도는 1900년부터 2200년 사이여야 합니다.");
        }

        var providerResult = await koreanHolidayProvider.FetchAsync(targetYear, cancellationToken);
        if (!providerResult.IsConfigured)
        {
            return new SyncKoreanHolidaysResponse(targetYear, "KR", 0, false, providerResult.Message);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var syncedAt = timeProvider.GetUtcNow();
        var upserted = 0;
        foreach (var holiday in providerResult.Holidays)
        {
            await using var command = new NpgsqlCommand("""
                insert into system_holidays (
                    holiday_date,
                    name,
                    country_code,
                    source,
                    source_key,
                    holiday_type,
                    is_active,
                    synced_at_utc,
                    updated_at_utc
                )
                values (
                    @holiday_date,
                    @name,
                    @country_code,
                    @source,
                    @source_key,
                    @holiday_type,
                    true,
                    @synced_at_utc,
                    @updated_at_utc
                )
                on conflict (country_code, holiday_date, source_key) do update
                set name = excluded.name,
                    source = excluded.source,
                    holiday_type = excluded.holiday_type,
                    is_active = true,
                    synced_at_utc = excluded.synced_at_utc,
                    updated_at_utc = excluded.updated_at_utc;
                """, connection, transaction);
            command.Parameters.AddWithValue("holiday_date", holiday.HolidayDate);
            command.Parameters.AddWithValue("name", holiday.Name);
            command.Parameters.AddWithValue("country_code", NormalizeCountryCode(holiday.CountryCode));
            command.Parameters.AddWithValue("source", holiday.Source);
            command.Parameters.AddWithValue("source_key", holiday.SourceKey);
            command.Parameters.AddWithValue("holiday_type", SystemHolidayTypes.Normalize(holiday.HolidayType));
            command.Parameters.AddWithValue("synced_at_utc", syncedAt);
            command.Parameters.AddWithValue("updated_at_utc", syncedAt);
            upserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new SyncKoreanHolidaysResponse(
            targetYear,
            "KR",
            upserted,
            true,
            $"{targetYear}년 한국 공휴일/국경일 {upserted}건을 동기화했습니다.");
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

public interface IKoreanHolidayProvider
{
    Task<KoreanHolidayProviderResult> FetchAsync(int year, CancellationToken cancellationToken);
}

public sealed class OfficialKoreanHolidayProvider(HttpClient httpClient, IConfiguration configuration) : IKoreanHolidayProvider
{
    private const string DefaultServiceBase = "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService";
    private const string DefaultPublicHolidayEndpoint = $"{DefaultServiceBase}/getRestDeInfo";
    private const string DefaultNationalHolidayEndpoint = $"{DefaultServiceBase}/getHoliDeInfo";

    public async Task<KoreanHolidayProviderResult> FetchAsync(int year, CancellationToken cancellationToken)
    {
        var serviceKey = configuration["KoreaHolidayApi:ServiceKey"]
            ?? configuration["KOREA_HOLIDAY_API_SERVICE_KEY"]
            ?? configuration["KoreanHolidaySync:ServiceKey"]
            ?? configuration["KOREAN_HOLIDAY_SERVICE_KEY"];
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            return new KoreanHolidayProviderResult(
                false,
                Array.Empty<SystemHolidayUpsert>(),
                "공휴일 API 인증키가 설정되지 않았습니다. 운영 환경변수 KOREA_HOLIDAY_API_SERVICE_KEY를 등록해 주세요.");
        }

        var publicHolidayEndpoint = configuration["KoreaHolidayApi:PublicHolidayEndpoint"]
            ?? configuration["KoreanHolidaySync:PublicHolidayEndpoint"]
            ?? configuration["KoreaHolidayApi:Endpoint"]
            ?? configuration["KoreanHolidaySync:Endpoint"]
            ?? DefaultPublicHolidayEndpoint;
        var nationalHolidayEndpoint = configuration["KoreaHolidayApi:NationalHolidayEndpoint"]
            ?? configuration["KoreanHolidaySync:NationalHolidayEndpoint"]
            ?? DefaultNationalHolidayEndpoint;
        var endpointDefinitions = new[]
        {
            new HolidayEndpoint(publicHolidayEndpoint, "OfficialApi:PublicHoliday", RequireHolidayFlag: true),
            new HolidayEndpoint(nationalHolidayEndpoint, "OfficialApi:NationalHoliday", RequireHolidayFlag: false)
        };
        var holidays = new List<SystemHolidayUpsert>();
        foreach (var endpoint in endpointDefinitions)
        {
            for (var month = 1; month <= 12; month += 1)
            {
                var uri = BuildRequestUri(endpoint.Url, serviceKey, year, month);
                using var response = await httpClient.GetAsync(uri, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new KoreanHolidayProviderResult(
                        false,
                        Array.Empty<SystemHolidayUpsert>(),
                        "공식 공휴일/국경일 API 응답을 확인할 수 없습니다. API key와 네트워크 상태를 확인해 주세요.");
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                holidays.AddRange(ParseHolidayXml(content, endpoint.Source, endpoint.RequireHolidayFlag));
            }
        }

        return new KoreanHolidayProviderResult(
            true,
            holidays
                .GroupBy(holiday => new { holiday.CountryCode, holiday.HolidayDate, holiday.SourceKey })
                .Select(group => group.First())
                .ToArray(),
            "공식 공휴일/국경일 API 동기화 준비가 완료되었습니다.");
    }

    private static Uri BuildRequestUri(string endpoint, string serviceKey, int year, int month)
    {
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var encodedServiceKey = serviceKey.Contains('%', StringComparison.Ordinal)
            ? serviceKey
            : Uri.EscapeDataString(serviceKey);
        var query = string.Create(CultureInfo.InvariantCulture, $"ServiceKey={encodedServiceKey}&solYear={year}&solMonth={month:D2}&numOfRows=100");
        return new Uri($"{endpoint}{separator}{query}", UriKind.Absolute);
    }

    private static IReadOnlyList<SystemHolidayUpsert> ParseHolidayXml(string content, string source, bool requireHolidayFlag)
    {
        var document = XDocument.Parse(content);
        return document
            .Descendants("item")
            .Select(item =>
            {
                var name = item.Element("dateName")?.Value?.Trim();
                var locdate = item.Element("locdate")?.Value?.Trim();
                var isHoliday = item.Element("isHoliday")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(name)
                    || string.IsNullOrWhiteSpace(locdate)
                    || (requireHolidayFlag && !string.Equals(isHoliday, "Y", StringComparison.OrdinalIgnoreCase))
                    || !DateOnly.TryParseExact(locdate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return null;
                }

                return new SystemHolidayUpsert(
                    date,
                    name,
                    "KR",
                    source,
                    $"{source}:{locdate}:{name}",
                    ClassifyHolidayType(name, source));
            })
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    private static string ClassifyHolidayType(string name, string source)
    {
        if (source.Contains("Company", StringComparison.OrdinalIgnoreCase))
        {
            return SystemHolidayTypes.Company;
        }

        if (name.Contains("대체", StringComparison.Ordinal))
        {
            return SystemHolidayTypes.Substitute;
        }

        if (name.Contains("임시", StringComparison.Ordinal))
        {
            return SystemHolidayTypes.Temporary;
        }

        return SystemHolidayTypes.National;
    }

    private sealed record HolidayEndpoint(string Url, string Source, bool RequireHolidayFlag);
}
