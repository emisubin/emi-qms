using System.Data;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Sales;

public sealed class SalesKpiStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo SeoulTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    public async Task<SalesKpiResponse> GetAsync(
        int? requestedYear,
        string? requestedCurrency,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), SeoulTimeZone).DateTime);
        var year = requestedYear ?? today.Year;
        ValidateYear(year);
        var normalizedCurrency = NormalizeCurrency(requestedCurrency);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var defaultCurrency = await ResolveDefaultCurrencyAsync(connection, year, scope, cancellationToken);
        var currency = normalizedCurrency ?? defaultCurrency;
        var availableYears = await ReadAvailableYearsAsync(connection, today.Year, scope, cancellationToken);
        var availableCurrencies = await ReadAvailableCurrenciesAsync(connection, year, currency, scope, cancellationToken);

        var revenue = await ReadRevenueAsync(connection, year, currency, scope, cancellationToken);
        var targets = await ReadTargetsAsync(connection, year, currency, cancellationToken);
        var months = Enumerable.Range(1, 12)
            .Select(month => new SalesKpiMonthResponse(
                month,
                revenue.TryGetValue(month, out var value) ? value.Amount : 0m,
                targets.TryGetValue(month, out var target) ? target.Amount : null,
                revenue.TryGetValue(month, out value) ? value.Count : 0))
            .ToArray();

        var revenueTotal = months.Sum(item => item.RevenueAmount);
        var registeredTargets = months.Where(item => item.TargetAmount.HasValue).ToArray();
        decimal? targetTotal = registeredTargets.Length == 0 ? null : registeredTargets.Sum(item => item.TargetAmount!.Value);
        decimal? achievement = targetTotal is > 0m ? decimal.Round(revenueTotal / targetTotal.Value * 100m, 1) : null;
        decimal? remaining = targetTotal.HasValue ? Math.Max(targetTotal.Value - revenueTotal, 0m) : null;
        decimal? exceeded = targetTotal.HasValue ? Math.Max(revenueTotal - targetTotal.Value, 0m) : null;
        var currentMonth = year == today.Year ? months[today.Month - 1].RevenueAmount : 0m;
        var pipeline = await ReadPipelineAsync(connection, currency, scope, cancellationToken);
        var missingAmount = await ReadMissingAmountCountAsync(connection, year, scope, cancellationToken);

        return new SalesKpiResponse(
            year,
            currency,
            defaultCurrency,
            availableYears,
            availableCurrencies,
            months,
            new SalesKpiSummaryResponse(
                currentMonth,
                revenueTotal,
                targetTotal,
                registeredTargets.Length,
                achievement,
                remaining,
                exceeded),
            pipeline,
            missingAmount);
    }

    public async Task<SalesKpiMonthDetailResponse> GetMonthAsync(
        int year,
        int month,
        string currency,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        ValidateYear(year);
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month), "월은 1부터 12까지 입력해 주세요.");
        var normalizedCurrency = NormalizeCurrency(currency) ?? throw new ArgumentException("통화를 입력해 주세요.", nameof(currency));

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select project.id,
                   coalesce(project.project_code, project.project_number),
                   coalesce(project.project_title, project.name),
                   settlement.invoice_issued_date,
                   project.sales_amount
            from sales_settlements settlement
            join projects project on project.id = settlement.project_id
            where settlement.status = 'Completed'
              and project.deleted_at_utc is null
              and extract(year from settlement.invoice_issued_date)::int = @year
              and extract(month from settlement.invoice_issued_date)::int = @month
              and project.currency_code = @currency
              and project.sales_amount is not null
              and (@has_read_all or project.project_key = any(@project_keys))
            order by settlement.invoice_issued_date, project.project_code nulls last, project.project_number
            limit 500;
            """;
        command.Parameters.AddWithValue("year", year);
        command.Parameters.AddWithValue("month", month);
        command.Parameters.AddWithValue("currency", normalizedCurrency);
        AddScope(command, scope);
        var projects = new List<SalesKpiProjectResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(new SalesKpiProjectResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateOnly>(3),
                reader.GetDecimal(4)));
        }
        return new SalesKpiMonthDetailResponse(year, month, normalizedCurrency, projects);
    }

    public async Task<SalesTargetsResponse> GetTargetsAsync(int year, string currency, CancellationToken cancellationToken)
    {
        ValidateYear(year);
        var normalizedCurrency = NormalizeCurrency(currency) ?? throw new ArgumentException("통화를 입력해 주세요.", nameof(currency));
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var targets = await ReadTargetsAsync(connection, year, normalizedCurrency, cancellationToken);
        return TargetResponse(year, normalizedCurrency, targets);
    }

    public async Task<SalesTargetsResponse> SaveTargetsAsync(
        SaveSalesTargetsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ValidateYear(request.Year);
        var currency = NormalizeCurrency(request.Currency) ?? throw new ArgumentException("통화를 입력해 주세요.", nameof(request.Currency));
        if (request.Months.Count > 12 || request.Months.Select(item => item.Month).Distinct().Count() != request.Months.Count)
            throw new ArgumentException("월은 중복 없이 입력해 주세요.", nameof(request.Months));
        foreach (var item in request.Months)
        {
            if (item.Month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(request.Months), "월은 1부터 12까지 입력해 주세요.");
            if (item.Amount < 0m) throw new ArgumentOutOfRangeException(nameof(request.Months), "목표 금액은 0 이상이어야 합니다.");
            if (item.ExpectedVersion is < 1) throw new ArgumentOutOfRangeException(nameof(request.Months), "현재 버전을 확인해 주세요.");
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        foreach (var item in request.Months.OrderBy(item => item.Month))
        {
            var current = await LockTargetAsync(connection, transaction, request.Year, item.Month, currency, cancellationToken);
            if (current is null && item.ExpectedVersion is not null)
                throw new DBConcurrencyException("영업 목표가 변경되었습니다. 새로고침 후 다시 시도해 주세요.");
            if (current is not null && current.Value.Version != item.ExpectedVersion)
                throw new DBConcurrencyException("영업 목표가 변경되었습니다. 새로고침 후 다시 시도해 주세요.");
            if (current is not null && current.Value.Amount == item.Amount) continue;

            var targetId = current?.Id ?? Guid.NewGuid();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = current is null
                    ? """
                      insert into sales_monthly_targets (
                          id, target_year, target_month, currency_code, amount,
                          created_by_user_id, updated_by_user_id)
                      values (@id, @year, @month, @currency, @amount, @actor, @actor);
                      """
                    : """
                      update sales_monthly_targets
                      set amount=@amount, version=version+1, updated_by_user_id=@actor, updated_at_utc=now()
                      where id=@id;
                      """;
                command.Parameters.AddWithValue("id", targetId);
                command.Parameters.AddWithValue("year", request.Year);
                command.Parameters.AddWithValue("month", item.Month);
                command.Parameters.AddWithValue("currency", currency);
                command.Parameters.AddWithValue("amount", item.Amount);
                command.Parameters.AddWithValue("actor", actorUserId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var audit = connection.CreateCommand())
            {
                audit.Transaction = transaction;
                audit.CommandText = """
                    insert into sales_monthly_target_audit_events (
                        target_id, target_year, target_month, currency_code, action,
                        previous_amount, next_amount, actor_user_id)
                    values (@id, @year, @month, @currency, @action, @previous, @next, @actor);
                    """;
                audit.Parameters.AddWithValue("id", targetId);
                audit.Parameters.AddWithValue("year", request.Year);
                audit.Parameters.AddWithValue("month", item.Month);
                audit.Parameters.AddWithValue("currency", currency);
                audit.Parameters.AddWithValue("action", current is null ? "Create" : "Update");
                audit.Parameters.Add("previous", NpgsqlDbType.Numeric).Value = current?.Amount ?? (object)DBNull.Value;
                audit.Parameters.AddWithValue("next", item.Amount);
                audit.Parameters.AddWithValue("actor", actorUserId);
                await audit.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetTargetsAsync(request.Year, currency, cancellationToken);
    }

    private static async Task<Dictionary<int, (decimal Amount, int Count)>> ReadRevenueAsync(
        NpgsqlConnection connection, int year, string currency, ProjectAccessScope scope, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select extract(month from settlement.invoice_issued_date)::int, sum(project.sales_amount), count(*)::int
            from sales_settlements settlement
            join projects project on project.id=settlement.project_id
            where settlement.status='Completed' and project.deleted_at_utc is null
              and extract(year from settlement.invoice_issued_date)::int=@year
              and project.currency_code=@currency and project.sales_amount is not null
              and (@has_read_all or project.project_key=any(@project_keys))
            group by 1;
            """;
        command.Parameters.AddWithValue("year", year);
        command.Parameters.AddWithValue("currency", currency);
        AddScope(command, scope);
        var result = new Dictionary<int, (decimal, int)>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result[reader.GetInt32(0)] = (reader.GetDecimal(1), reader.GetInt32(2));
        return result;
    }

    private static async Task<Dictionary<int, (Guid Id, decimal Amount, int Version)>> ReadTargetsAsync(
        NpgsqlConnection connection, int year, string currency, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select id,target_month,amount,version from sales_monthly_targets where target_year=@year and currency_code=@currency order by target_month;";
        command.Parameters.AddWithValue("year", year);
        command.Parameters.AddWithValue("currency", currency);
        var result = new Dictionary<int, (Guid, decimal, int)>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result[reader.GetInt32(1)] = (reader.GetGuid(0), reader.GetDecimal(2), reader.GetInt32(3));
        return result;
    }

    private static async Task<(Guid Id, decimal Amount, int Version)?> LockTargetAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, int year, int month, string currency, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id,amount,version from sales_monthly_targets where target_year=@year and target_month=@month and currency_code=@currency for update;";
        command.Parameters.AddWithValue("year", year);
        command.Parameters.AddWithValue("month", month);
        command.Parameters.AddWithValue("currency", currency);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? (reader.GetGuid(0), reader.GetDecimal(1), reader.GetInt32(2)) : null;
    }

    private static async Task<string> ResolveDefaultCurrencyAsync(NpgsqlConnection connection, int year, ProjectAccessScope scope, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select project.currency_code
            from sales_settlements settlement join projects project on project.id=settlement.project_id
            where settlement.status='Completed' and project.deleted_at_utc is null
              and extract(year from settlement.invoice_issued_date)::int=@year
              and project.sales_amount is not null and project.currency_code is not null
              and (@has_read_all or project.project_key=any(@project_keys))
            group by project.currency_code order by sum(project.sales_amount) desc, project.currency_code limit 1;
            """;
        command.Parameters.AddWithValue("year", year);
        AddScope(command, scope);
        return (string?)await command.ExecuteScalarAsync(token) ?? "KRW";
    }

    private static async Task<IReadOnlyList<int>> ReadAvailableYearsAsync(NpgsqlConnection connection, int currentYear, ProjectAccessScope scope, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select distinct year_value from (
              select extract(year from settlement.invoice_issued_date)::int year_value
              from sales_settlements settlement join projects project on project.id=settlement.project_id
              where settlement.status='Completed' and project.deleted_at_utc is null
                and (@has_read_all or project.project_key=any(@project_keys))
              union select target_year from sales_monthly_targets
              union select @current_year
            ) years where year_value between 2000 and 2100 order by year_value desc;
            """;
        command.Parameters.AddWithValue("current_year", currentYear);
        AddScope(command, scope);
        var values = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) values.Add(reader.GetInt32(0));
        return values;
    }

    private static async Task<IReadOnlyList<string>> ReadAvailableCurrenciesAsync(NpgsqlConnection connection, int year, string selected, ProjectAccessScope scope, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select distinct currency from (
              select project.currency_code currency
              from sales_settlements settlement join projects project on project.id=settlement.project_id
              where settlement.status='Completed' and project.deleted_at_utc is null
                and extract(year from settlement.invoice_issued_date)::int=@year
                and project.currency_code is not null
                and (@has_read_all or project.project_key=any(@project_keys))
              union select currency_code from sales_monthly_targets where target_year=@year
              union select @selected
            ) currencies where currency ~ '^[A-Z]{3}$' order by currency;
            """;
        command.Parameters.AddWithValue("year", year);
        command.Parameters.AddWithValue("selected", selected);
        AddScope(command, scope);
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) values.Add(reader.GetString(0));
        return values;
    }

    private static async Task<SalesPipelineResponse> ReadPipelineAsync(NpgsqlConnection connection, string currency, ProjectAccessScope scope, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select coalesce(sum(sales_amount),0), count(*)::int from projects
            where status='Active' and deleted_at_utc is null and currency_code=@currency and sales_amount is not null
              and (@has_read_all or project_key=any(@project_keys));
            """;
        command.Parameters.AddWithValue("currency", currency);
        AddScope(command, scope);
        await using var reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        return new SalesPipelineResponse(reader.GetDecimal(0), reader.GetInt32(1));
    }

    private static async Task<int> ReadMissingAmountCountAsync(NpgsqlConnection connection, int year, ProjectAccessScope scope, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select count(*)::int from sales_settlements settlement join projects project on project.id=settlement.project_id
            where settlement.status='Completed' and project.deleted_at_utc is null
              and extract(year from settlement.invoice_issued_date)::int=@year
              and (project.sales_amount is null or project.currency_code is null)
              and (@has_read_all or project.project_key=any(@project_keys));
            """;
        command.Parameters.AddWithValue("year", year);
        AddScope(command, scope);
        return Convert.ToInt32(await command.ExecuteScalarAsync(token));
    }

    private static SalesTargetsResponse TargetResponse(int year, string currency, IReadOnlyDictionary<int, (Guid Id, decimal Amount, int Version)> targets)
        => new(year, currency, Enumerable.Range(1, 12).Select(month => targets.TryGetValue(month, out var target)
            ? new SalesTargetMonthResponse(month, target.Amount, target.Version)
            : new SalesTargetMonthResponse(month, null, null)).ToArray());

    private static void ValidateYear(int year)
    {
        if (year is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(year), "연도는 2000년부터 2100년까지 입력해 주세요.");
    }

    private static string? NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return null;
        var value = currency.Trim().ToUpperInvariant();
        if (value.Length != 3 || value.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("통화 코드는 영문 대문자 3자로 입력해 주세요.", nameof(currency));
        return value;
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("QMS database connection string is not configured.");
        return NpgsqlDataSource.Create(connectionString);
    }

    private static void AddScope(NpgsqlCommand command, ProjectAccessScope scope)
    {
        command.Parameters.AddWithValue("has_read_all", scope.HasProjectReadAll);
        command.Parameters.Add(new NpgsqlParameter<string[]>("project_keys", scope.ProjectKeys.ToArray()));
    }
}
