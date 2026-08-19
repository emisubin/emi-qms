using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.G2;

public sealed class G2OperationsStore(DatabaseConnectionStringProvider connectionStringProvider, TimeProvider timeProvider)
{
    private const int AdvisoryLockNamespace = 0x4732;
    private static readonly TimeZoneInfo SeoulTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    public async Task<G2HomeResponse> GetHomeAsync(int? requestedYear, int? requestedMonth, CancellationToken token)
    {
        var today = Today();
        var year = requestedYear ?? today.Year;
        var month = requestedMonth ?? today.Month;
        ValidateYearMonth(year, month);
        var from = new DateOnly(year, month, 1);
        var range = await ReadRangeAsync(from, from.AddMonths(1).AddDays(-1), token);
        return new(today, year, month, range.Days.Any(day => day.Inventory.HasValue), range.Days);
    }

    public async Task<G2RangeResponse> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        ValidateRange(from, to);
        return await ReadRangeAsync(from, to, token);
    }

    internal async Task SaveMetricsAsync(DateOnly date, IReadOnlyList<G2MetricChange> changes, Guid actor, CancellationToken token)
    {
        ValidateDate(date);
        var today = Today();
        if (changes.Count == 0) throw new ArgumentException("저장할 항목을 하나 이상 입력해 주세요.", nameof(changes));
        if (changes.Select(change => change.MetricCode).Distinct(StringComparer.Ordinal).Count() != changes.Count)
            throw new ArgumentException("같은 항목을 중복해서 저장할 수 없습니다.", nameof(changes));
        foreach (var change in changes)
        {
            if (!G2MetricCodes.All.Contains(change.MetricCode, StringComparer.Ordinal))
                throw new ArgumentException("지원하지 않는 G2 항목입니다.", nameof(changes));
            ValidateQuantity(change.Quantity, change.ExpectedVersion, nameof(changes));
        }

        await using var source = CreateDataSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await ExpireForecastMetricsAsync(connection, transaction, today, token);
        foreach (var change in changes.OrderBy(change => change.MetricCode, StringComparer.Ordinal))
        {
            await AcquireLockAsync(connection, transaction, MetricLockKey(date, change.MetricCode), token);
            var current = await LockMetricAsync(connection, transaction, date, change.MetricCode, token);
            EnsureVersion(current?.Version, change.ExpectedVersion, "G2 입력값이 다른 사용자에 의해 변경되었습니다. 새로고침 후 다시 시도해 주세요.");
            var isForecast = date > today && change.Quantity.HasValue;
            if (current is not null && current.Quantity == change.Quantity && current.IsForecast == isForecast) continue;

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = current is null
                ? "insert into g2_daily_metrics (work_date,metric_code,quantity,is_forecast,created_by_user_id,updated_by_user_id) values (@date,@code,@quantity,@is_forecast,@actor,@actor);"
                : "update g2_daily_metrics set quantity=@quantity,is_forecast=@is_forecast,version=version+1,updated_by_user_id=@actor,updated_at_utc=now() where id=@id;";
            command.Parameters.AddWithValue("date", date);
            command.Parameters.AddWithValue("code", change.MetricCode);
            command.Parameters.Add("quantity", NpgsqlDbType.Integer).Value = change.Quantity is null ? DBNull.Value : change.Quantity.Value;
            command.Parameters.AddWithValue("is_forecast", isForecast);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("id", current?.Id ?? Guid.Empty);
            await command.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    public async Task SaveInventoryCountAsync(DateOnly date, SaveG2InventoryCountRequest request, Guid actor, CancellationToken token)
    {
        ValidateDate(date);
        if (date > Today()) throw new ArgumentException("재고 실사는 오늘 또는 과거 날짜에만 입력할 수 있습니다.", nameof(date));
        ValidateQuantity(request.Quantity, request.ExpectedVersion, nameof(request));
        await using var source = CreateDataSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await AcquireLockAsync(connection, transaction, InventoryLockKey(date), token);
        var current = await LockInventoryAsync(connection, transaction, date, token);
        EnsureVersion(current?.Version, request.ExpectedVersion, "재고 실사값이 다른 사용자에 의해 변경되었습니다. 새로고침 후 다시 시도해 주세요.");
        if (current is null || current.Quantity != request.Quantity)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = current is null
                ? "insert into g2_inventory_counts (count_date,quantity,created_by_user_id,updated_by_user_id) values (@date,@quantity,@actor,@actor);"
                : "update g2_inventory_counts set quantity=@quantity,version=version+1,updated_by_user_id=@actor,updated_at_utc=now() where id=@id;";
            command.Parameters.AddWithValue("date", date);
            command.Parameters.AddWithValue("quantity", request.Quantity);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("id", current?.Id ?? Guid.Empty);
            await command.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    public async Task DeleteInventoryCountAsync(DateOnly date, int? expectedVersion, CancellationToken token)
    {
        ValidateDate(date);
        if (expectedVersion is < 1) throw new ArgumentException("현재 버전을 확인해 주세요.", nameof(expectedVersion));
        await using var source = CreateDataSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await AcquireLockAsync(connection, transaction, InventoryLockKey(date), token);
        var current = await LockInventoryAsync(connection, transaction, date, token);
        EnsureVersion(current?.Version, expectedVersion, "재고 실사값이 다른 사용자에 의해 변경되었습니다. 새로고침 후 다시 시도해 주세요.");
        if (current is not null)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "delete from g2_inventory_counts where id=@id;";
            command.Parameters.AddWithValue("id", current.Id);
            await command.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    public async Task SaveTargetAsync(string type, DateOnly date, SaveG2TargetRequest request, Guid actor, CancellationToken token)
    {
        ValidateDate(date);
        if (!G2TargetTypes.IsValid(type)) throw new ArgumentException("지원하지 않는 G2 목표 유형입니다.", nameof(type));
        ValidateQuantity(request.Quantity, request.ExpectedVersion, nameof(request));
        await using var source = CreateDataSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await AcquireLockAsync(connection, transaction, TargetLockKey(date, type), token);
        var current = await LockTargetAsync(connection, transaction, type, date, token);
        EnsureVersion(current?.Version, request.ExpectedVersion, "G2 목표가 다른 사용자에 의해 변경되었습니다. 새로고침 후 다시 시도해 주세요.");
        if (current is null || current.Quantity != request.Quantity)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = current is null
                ? "insert into g2_targets (target_type,effective_date,quantity,created_by_user_id,updated_by_user_id) values (@type,@date,@quantity,@actor,@actor);"
                : "update g2_targets set quantity=@quantity,version=version+1,updated_by_user_id=@actor,updated_at_utc=now() where id=@id;";
            command.Parameters.AddWithValue("type", type);
            command.Parameters.AddWithValue("date", date);
            command.Parameters.AddWithValue("quantity", request.Quantity);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("id", current?.Id ?? Guid.Empty);
            await command.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    private async Task<G2RangeResponse> ReadRangeAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        var today = Today();
        await using var source = CreateDataSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await ExpireForecastMetricsAsync(connection, null, today, token);
        var metrics = await ReadMetricsAsync(connection, from, to, token);
        var counts = await ReadCountsAsync(connection, from, to, token);
        var targets = await ReadTargetsAsync(connection, from, to, token);
        var balanceBefore = await ReadBalanceBeforeAsync(connection, from, token);
        var production = new Dictionary<DateOnly, long>();
        var delivery = new Dictionary<DateOnly, long>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            production[date] = (long)(Metric(metrics, date, G2MetricCodes.MorningProduction)?.Quantity ?? 0) + (Metric(metrics, date, G2MetricCodes.AfternoonProduction)?.Quantity ?? 0);
            delivery[date] = Metric(metrics, date, G2MetricCodes.Delivery)?.Quantity ?? 0;
        }
        var inventory = G2InventoryCalculator.Calculate(from, to, balanceBefore, counts.ToDictionary(row => row.Key, row => row.Value.Quantity), production, delivery);
        var dailyTargets = ExpandTargets(from, to, targets);
        var days = new List<G2DayResponse>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var mp = Metric(metrics, date, G2MetricCodes.MorningProduction);
            var ap = Metric(metrics, date, G2MetricCodes.AfternoonProduction);
            var me = Metric(metrics, date, G2MetricCodes.MorningEmiAttendance);
            var mc = Metric(metrics, date, G2MetricCodes.MorningContractorAttendance);
            var ae = Metric(metrics, date, G2MetricCodes.AfternoonEmiAttendance);
            var ac = Metric(metrics, date, G2MetricCodes.AfternoonContractorAttendance);
            var morningTotal = Sum(me?.Quantity, mc?.Quantity);
            var afternoonTotal = Sum(ae?.Quantity, ac?.Quantity);
            dailyTargets.TryGetValue((date, G2TargetTypes.DailyProduction), out var productionTarget);
            dailyTargets.TryGetValue((date, G2TargetTypes.Inventory), out var inventoryTarget);
            days.Add(new(
                date, date > today, ToResponse(mp), ToResponse(ap), ToResponse(Metric(metrics, date, G2MetricCodes.Delivery)),
                ToResponse(me), ToResponse(mc), ToResponse(ae), ToResponse(ac), Sum(mp?.Quantity, ap?.Quantity),
                morningTotal, afternoonTotal, Sum(morningTotal, afternoonTotal), inventory.GetValueOrDefault(date),
                counts.TryGetValue(date, out var count) ? ToResponse(count) : null,
                productionTarget is null ? null : ToResponse(productionTarget), inventoryTarget is null ? null : ToResponse(inventoryTarget)));
        }
        return new(today, from, to, days);
    }

    private static async Task<Dictionary<(DateOnly, string), MetricRow>> ReadMetricsAsync(NpgsqlConnection connection, DateOnly from, DateOnly to, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select m.id,m.work_date,m.metric_code,m.quantity,m.is_forecast,m.version,m.updated_at_utc,u.display_name from g2_daily_metrics m join qms_users u on u.id=m.updated_by_user_id where m.work_date between @from and @to order by m.work_date,m.metric_code;";
        command.Parameters.AddWithValue("from", from); command.Parameters.AddWithValue("to", to);
        var result = new Dictionary<(DateOnly, string), MetricRow>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var row = new MetricRow(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.GetBoolean(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7));
            result[(row.Date, row.Code)] = row;
        }
        return result;
    }

    private static async Task<Dictionary<DateOnly, InventoryRow>> ReadCountsAsync(NpgsqlConnection connection, DateOnly from, DateOnly to, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select c.id,c.count_date,c.quantity,c.version,c.updated_at_utc,u.display_name from g2_inventory_counts c join qms_users u on u.id=c.updated_by_user_id where c.count_date between @from and @to order by c.count_date;";
        command.Parameters.AddWithValue("from", from); command.Parameters.AddWithValue("to", to);
        var result = new Dictionary<DateOnly, InventoryRow>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var row = new InventoryRow(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetFieldValue<DateTimeOffset>(4), reader.GetString(5));
            result[row.Date] = row;
        }
        return result;
    }

    private static async Task<IReadOnlyList<TargetRow>> ReadTargetsAsync(NpgsqlConnection connection, DateOnly from, DateOnly to, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with prior as (
              select distinct on (target_type) id,target_type,effective_date,quantity,version,updated_at_utc,updated_by_user_id
              from g2_targets where effective_date < @from order by target_type,effective_date desc
            ), selected as (
              select * from prior union all
              select id,target_type,effective_date,quantity,version,updated_at_utc,updated_by_user_id from g2_targets where effective_date between @from and @to
            )
            select s.id,s.target_type,s.effective_date,s.quantity,s.version,s.updated_at_utc,u.display_name
            from selected s join qms_users u on u.id=s.updated_by_user_id order by s.effective_date,s.target_type;
            """;
        command.Parameters.AddWithValue("from", from); command.Parameters.AddWithValue("to", to);
        var result = new List<TargetRow>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetFieldValue<DateTimeOffset>(5), reader.GetString(6)));
        return result;
    }

    private static async Task<long?> ReadBalanceBeforeAsync(NpgsqlConnection connection, DateOnly from, CancellationToken token)
    {
        DateOnly checkpointDate;
        long balance;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select count_date,quantity::bigint from g2_inventory_counts where count_date < @from order by count_date desc limit 1;";
            command.Parameters.AddWithValue("from", from);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) return null;
            checkpointDate = reader.GetFieldValue<DateOnly>(0); balance = reader.GetInt64(1);
        }
        await using var sum = connection.CreateCommand();
        sum.CommandText = "select coalesce(sum(case metric_code when 'MorningProduction' then coalesce(quantity,0) when 'AfternoonProduction' then coalesce(quantity,0) when 'Delivery' then -coalesce(quantity,0) else 0 end),0)::bigint from g2_daily_metrics where work_date > @checkpoint and work_date < @from;";
        sum.Parameters.AddWithValue("checkpoint", checkpointDate); sum.Parameters.AddWithValue("from", from);
        return checked(balance + (long)(await sum.ExecuteScalarAsync(token) ?? 0L));
    }

    private static Dictionary<(DateOnly, string), TargetRow?> ExpandTargets(DateOnly from, DateOnly to, IReadOnlyList<TargetRow> targets)
    {
        var result = new Dictionary<(DateOnly, string), TargetRow?>();
        var changes = targets.GroupBy(t => t.Date).ToDictionary(g => g.Key, g => g.ToArray());
        var current = targets.Where(t => t.Date < from).ToDictionary(t => t.Type, StringComparer.Ordinal);
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (changes.TryGetValue(date, out var dateChanges)) foreach (var target in dateChanges) current[target.Type] = target;
            result[(date, G2TargetTypes.DailyProduction)] = current.GetValueOrDefault(G2TargetTypes.DailyProduction);
            result[(date, G2TargetTypes.Inventory)] = current.GetValueOrDefault(G2TargetTypes.Inventory);
        }
        return result;
    }

    private static async Task AcquireLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int key, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select pg_advisory_xact_lock(@namespace,@key);";
        command.Parameters.AddWithValue("namespace", AdvisoryLockNamespace); command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task ExpireForecastMetricsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, DateOnly today, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update g2_daily_metrics
            set quantity=null,is_forecast=false,version=version+1,updated_at_utc=now()
            where is_forecast and work_date <= @today;
            """;
        command.Parameters.AddWithValue("today", today);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<MetricRow?> LockMetricAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, DateOnly date, string code, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select id,quantity,is_forecast,version from g2_daily_metrics where work_date=@date and metric_code=@code for update;";
        command.Parameters.AddWithValue("date", date); command.Parameters.AddWithValue("code", code);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(reader.GetGuid(0), date, code, reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetBoolean(2), reader.GetInt32(3), default, "") : null;
    }

    private static async Task<InventoryRow?> LockInventoryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, DateOnly date, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select id,quantity,version from g2_inventory_counts where count_date=@date for update;"; command.Parameters.AddWithValue("date", date);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(reader.GetGuid(0), date, reader.GetInt32(1), reader.GetInt32(2), default, "") : null;
    }

    private static async Task<TargetRow?> LockTargetAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string type, DateOnly date, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select id,quantity,version from g2_targets where target_type=@type and effective_date=@date for update;";
        command.Parameters.AddWithValue("type", type); command.Parameters.AddWithValue("date", date);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(reader.GetGuid(0), type, date, reader.GetInt32(1), reader.GetInt32(2), default, "") : null;
    }

    private static G2MetricValueResponse? ToResponse(MetricRow? row) => row is null ? null : new(row.Quantity, row.Version, row.UpdatedAt, row.UpdatedBy);
    private static G2InventoryCountResponse ToResponse(InventoryRow row) => new(row.Quantity, row.Version, row.UpdatedAt, row.UpdatedBy);
    private static G2TargetResponse ToResponse(TargetRow row) => new(row.Type, row.Date, row.Quantity, row.Version, row.UpdatedAt, row.UpdatedBy);
    private static MetricRow? Metric(IReadOnlyDictionary<(DateOnly, string), MetricRow> metrics, DateOnly date, string code) => metrics.GetValueOrDefault((date, code));
    private static long? Sum(int? left, int? right) => left.HasValue || right.HasValue ? (long)(left ?? 0) + (right ?? 0) : null;
    private static long? Sum(long? left, long? right) => left.HasValue || right.HasValue ? (left ?? 0) + (right ?? 0) : null;
    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), SeoulTimeZone).DateTime);
    private static void EnsureVersion(int? current, int? expected, string message) { if (current != expected) throw new DBConcurrencyException(message); }
    private static void ValidateQuantity(int? quantity, int? version, string name) { if (quantity is < 0) throw new ArgumentOutOfRangeException(name, "수량은 0 이상이어야 합니다."); if (version is < 1) throw new ArgumentOutOfRangeException(name, "현재 버전을 확인해 주세요."); }
    private static void ValidateDate(DateOnly date) { if (date.Year < 2000) throw new ArgumentOutOfRangeException(nameof(date), "날짜는 2000년 이후로 선택해 주세요."); }
    private static void ValidateRange(DateOnly from, DateOnly to) { ValidateDate(from); ValidateDate(to); if (to < from) throw new ArgumentException("종료일은 시작일보다 빠를 수 없습니다.", nameof(to)); if (to.DayNumber - from.DayNumber > 365) throw new ArgumentException("조회 기간은 최대 366일입니다.", nameof(to)); }
    private static void ValidateYearMonth(int year, int month) { if (year is < 2000 or > 9999) throw new ArgumentOutOfRangeException(nameof(year), "연도는 2000년 이후로 선택해 주세요."); if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month), "월은 1월부터 12월까지 선택해 주세요."); }
    private static int MetricLockKey(DateOnly date, string code) => checked(date.DayNumber * 16 + code switch { G2MetricCodes.MorningProduction => 0, G2MetricCodes.AfternoonProduction => 1, G2MetricCodes.Delivery => 2, G2MetricCodes.MorningEmiAttendance => 3, G2MetricCodes.MorningContractorAttendance => 4, G2MetricCodes.AfternoonEmiAttendance => 5, G2MetricCodes.AfternoonContractorAttendance => 6, _ => throw new ArgumentOutOfRangeException(nameof(code)) });
    private static int InventoryLockKey(DateOnly date) => checked(date.DayNumber * 16 + 8);
    private static int TargetLockKey(DateOnly date, string type) => checked(date.DayNumber * 16 + (type == G2TargetTypes.DailyProduction ? 9 : 10));
    private NpgsqlDataSource CreateDataSource() { var value = connectionStringProvider.GetConnectionString(); return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException("QMS database connection is not configured.") : NpgsqlDataSource.Create(value); }
    private sealed record MetricRow(Guid Id, DateOnly Date, string Code, int? Quantity, bool IsForecast, int Version, DateTimeOffset UpdatedAt, string UpdatedBy);
    private sealed record InventoryRow(Guid Id, DateOnly Date, int Quantity, int Version, DateTimeOffset UpdatedAt, string UpdatedBy);
    private sealed record TargetRow(Guid Id, string Type, DateOnly Date, int Quantity, int Version, DateTimeOffset UpdatedAt, string UpdatedBy);
}
