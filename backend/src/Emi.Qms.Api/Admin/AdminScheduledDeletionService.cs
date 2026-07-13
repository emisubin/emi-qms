using Emi.Qms.Api.Identity;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Admin;

public sealed class AdminScheduledDeletionService(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider) : IAdminDeletionPurgeService
{
    private static readonly string[] UserReferenceColumns =
    [
        "user_id",
        "assigned_user_id",
        "sales_owner_user_id",
        "created_by_user_id",
        "updated_by_user_id",
        "changed_by_user_id",
        "started_by_user_id",
        "completed_by_user_id",
        "cancelled_by_user_id",
        "deleted_by_user_id",
        "recipient_user_id",
        "actual_user_id",
        "effective_user_id",
        "receipt_completed_by_user_id"
    ];

    public async Task<AdminScheduledDeletionPurgeResult> PurgeDueAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var purgedUsers = await PurgeUsersAsync(connection, transaction, now, cancellationToken);
        var purgedDepartments = await PurgeDepartmentsAsync(connection, transaction, now, cancellationToken);
        var purgedHolidays = await PurgeHolidaysAsync(connection, transaction, now, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new AdminScheduledDeletionPurgeResult(
            purgedUsers.PurgedCount,
            purgedDepartments.PurgedCount,
            purgedHolidays.PurgedCount,
            purgedUsers.BlockedCount + purgedDepartments.BlockedCount);
    }

    public async Task<AdminPurgeActionResult> PurgeUserNowAsync(Guid userId, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var row = await ReadRowByIdAsync(connection, transaction, "qms_users", userId, cancellationToken);
        if (row is null)
        {
            return new AdminPurgeActionResult("Failed", "사용자를 찾을 수 없습니다.");
        }

        var guardResult = await ActiveSystemAdministratorInvariantGuard.CheckPurgeRemovalAsync(
            connection,
            transaction,
            userId,
            cancellationToken);
        if (guardResult == ActiveSystemAdministratorGuardResult.Rejected)
        {
            return new AdminPurgeActionResult(
                "Failed",
                ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage);
        }

        var result = await PurgeUserRowAsync(connection, transaction, row, now, changedByUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AdminPurgeActionResult> PurgeDepartmentNowAsync(Guid departmentId, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var row = await ReadRowByIdAsync(connection, transaction, "departments", departmentId, cancellationToken);
        if (row is null)
        {
            return new AdminPurgeActionResult("Failed", "부서를 찾을 수 없습니다.");
        }

        var result = await PurgeDepartmentRowAsync(connection, transaction, row, now, changedByUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AdminPurgeActionResult> PurgeHolidayNowAsync(Guid holidayId, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var row = await ReadRowByIdAsync(connection, transaction, "system_holidays", holidayId, cancellationToken);
        if (row is null)
        {
            return new AdminPurgeActionResult("Failed", "휴일을 찾을 수 없습니다.");
        }

        var result = await PurgeHolidayRowAsync(connection, transaction, row, now, changedByUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<PurgeEntityResult> PurgeUsersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var users = await ReadDueRowsAsync(connection, transaction, "qms_users", now, cancellationToken);
        var result = new PurgeEntityResult();
        foreach (var user in users)
        {
            var guardResult = await ActiveSystemAdministratorInvariantGuard.CheckPurgeRemovalAsync(
                connection,
                transaction,
                user.Id,
                cancellationToken);
            if (guardResult == ActiveSystemAdministratorGuardResult.Rejected)
            {
                throw new InvalidOperationException(
                    ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage);
            }

            result.Add(await PurgeUserRowAsync(connection, transaction, user, now, null, cancellationToken));
        }

        return result;
    }

    private async Task<PurgeEntityResult> PurgeDepartmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var departments = await ReadDueRowsAsync(connection, transaction, "departments", now, cancellationToken);
        var result = new PurgeEntityResult();
        foreach (var department in departments)
        {
            result.Add(await PurgeDepartmentRowAsync(connection, transaction, department, now, null, cancellationToken));
        }

        return result;
    }

    private async Task<PurgeEntityResult> PurgeHolidaysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var holidays = await ReadDueRowsAsync(connection, transaction, "system_holidays", now, cancellationToken);
        var result = new PurgeEntityResult();
        foreach (var holiday in holidays)
        {
            result.Add(await PurgeHolidayRowAsync(connection, transaction, holiday, now, null, cancellationToken));
        }

        return result;
    }

    private static async Task<AdminPurgeActionResult> PurgeUserRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PurgeDueRow user,
        DateTimeOffset now,
        Guid? changedByUserId,
        CancellationToken cancellationToken)
    {
        if (await HasColumnReferenceAsync(connection, transaction, user.Id, "qms_users", UserReferenceColumns, cancellationToken))
        {
            const string reason = "업무, 알림 또는 이력에서 참조 중인 사용자라 완전 삭제가 보류되었습니다.";
            await MarkPurgeBlockedAsync(connection, transaction, "qms_users", user.Id, reason, now, cancellationToken);
            await InsertPurgeBlockedLogAsync(connection, transaction, "User", user.Id, user.Payload, reason, now, changedByUserId, cancellationToken);
            return new AdminPurgeActionResult("PurgeBlocked", reason);
        }

        await ExecuteDeleteAsync(connection, transaction, "qms_users", user.Id, cancellationToken);
        await InsertPurgeLogAsync(connection, transaction, "User", user.Id, user.Payload, now, changedByUserId, cancellationToken);
        return new AdminPurgeActionResult("Purged", "완전 삭제했습니다.");
    }

    private static async Task<AdminPurgeActionResult> PurgeDepartmentRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PurgeDueRow department,
        DateTimeOffset now,
        Guid? changedByUserId,
        CancellationToken cancellationToken)
    {
        if (await HasColumnReferenceAsync(connection, transaction, department.Id, "departments", ["department_id"], cancellationToken))
        {
            const string reason = "해당 부서를 사용하는 사용자가 있어 완전 삭제할 수 없습니다.";
            await MarkPurgeBlockedAsync(connection, transaction, "departments", department.Id, reason, now, cancellationToken);
            await InsertPurgeBlockedLogAsync(connection, transaction, "Department", department.Id, department.Payload, reason, now, changedByUserId, cancellationToken);
            return new AdminPurgeActionResult("PurgeBlocked", reason);
        }

        await ExecuteDeleteAsync(connection, transaction, "departments", department.Id, cancellationToken);
        await InsertPurgeLogAsync(connection, transaction, "Department", department.Id, department.Payload, now, changedByUserId, cancellationToken);
        return new AdminPurgeActionResult("Purged", "완전 삭제했습니다.");
    }

    private static async Task<AdminPurgeActionResult> PurgeHolidayRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PurgeDueRow holiday,
        DateTimeOffset now,
        Guid? changedByUserId,
        CancellationToken cancellationToken)
    {
        await ExecuteDeleteAsync(connection, transaction, "system_holidays", holiday.Id, cancellationToken);
        await InsertPurgeLogAsync(connection, transaction, "Holiday", holiday.Id, holiday.Payload, now, changedByUserId, cancellationToken);
        return new AdminPurgeActionResult("Purged", "완전 삭제했습니다.");
    }

    private static async Task<IReadOnlyList<PurgeDueRow>> ReadDueRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select id, row_to_json({QuoteIdentifier(tableName)}.*)::text
            from {QuoteIdentifier(tableName)}
            where deletion_requested_at_utc is not null
              and scheduled_hard_delete_at_utc <= @now
            order by id
            for update skip locked;
            """;
        command.Parameters.AddWithValue("now", now);

        var rows = new List<PurgeDueRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PurgeDueRow(reader.GetGuid(0), reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<PurgeDueRow?> ReadRowByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select id, row_to_json({QuoteIdentifier(tableName)}.*)::text
            from {QuoteIdentifier(tableName)}
            where id = @id
              and (deletion_requested_at_utc is not null or purge_blocked_at_utc is not null)
            for update;
            """;
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PurgeDueRow(reader.GetGuid(0), reader.GetString(1))
            : null;
    }

    private static async Task<bool> HasColumnReferenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string excludedTableName,
        IReadOnlyList<string> columnNames,
        CancellationToken cancellationToken)
    {
        await using var columnsCommand = connection.CreateCommand();
        columnsCommand.Transaction = transaction;
        columnsCommand.CommandText = """
            select table_schema, table_name, column_name
            from information_schema.columns
            where table_schema = 'public'
              and table_name <> @excluded_table_name
              and column_name = any(@column_names);
            """;
        columnsCommand.Parameters.AddWithValue("excluded_table_name", excludedTableName);
        columnsCommand.Parameters.AddWithValue("column_names", columnNames.ToArray());

        var candidates = new List<(string Schema, string Table, string Column)>();
        await using (var reader = await columnsCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                candidates.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        foreach (var candidate in candidates)
        {
            await using var existsCommand = connection.CreateCommand();
            existsCommand.Transaction = transaction;
            existsCommand.CommandText = $"""
                select exists(
                    select 1
                    from {QuoteIdentifier(candidate.Schema)}.{QuoteIdentifier(candidate.Table)}
                    where {QuoteIdentifier(candidate.Column)} = @id
                    limit 1
                );
                """;
            existsCommand.Parameters.AddWithValue("id", id);
            if (await existsCommand.ExecuteScalarAsync(cancellationToken) is true)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task MarkPurgeBlockedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        Guid id,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            update {QuoteIdentifier(tableName)}
            set purge_blocked_at_utc = @purge_blocked_at_utc,
                purge_blocked_reason = @purge_blocked_reason
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("purge_blocked_at_utc", now);
        command.Parameters.AddWithValue("purge_blocked_reason", reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteDeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"delete from {QuoteIdentifier(tableName)} where id = @id;";
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPurgeLogAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string entityType,
        Guid entityId,
        string beforeJson,
        DateTimeOffset now,
        Guid? changedByUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into admin_master_change_logs (
                entity_type, entity_id, action, before_json, after_json, reason, changed_by_user_id, changed_at_utc
            )
            values (
                @entity_type, @entity_id, 'Purged', @before_json, null, @reason, @changed_by_user_id, @changed_at_utc
            );
            """;
        command.Parameters.AddWithValue("entity_type", entityType);
        command.Parameters.AddWithValue("entity_id", entityId);
        command.Parameters.AddWithValue("before_json", beforeJson);
        command.Parameters.Add(new NpgsqlParameter("reason", NpgsqlDbType.Text) { Value = changedByUserId is null ? "삭제 예약 후 7일 경과로 완전 삭제" : "관리자 즉시 완전 삭제" });
        command.Parameters.Add(new NpgsqlParameter("changed_by_user_id", NpgsqlDbType.Uuid) { Value = changedByUserId ?? (object)DBNull.Value });
        command.Parameters.AddWithValue("changed_at_utc", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPurgeBlockedLogAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string entityType,
        Guid entityId,
        string beforeJson,
        string reason,
        DateTimeOffset now,
        Guid? changedByUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into admin_master_change_logs (
                entity_type, entity_id, action, before_json, after_json, reason, changed_by_user_id, changed_at_utc
            )
            values (
                @entity_type, @entity_id, 'PurgeBlocked', @before_json, null, @reason, @changed_by_user_id, @changed_at_utc
            );
            """;
        command.Parameters.AddWithValue("entity_type", entityType);
        command.Parameters.AddWithValue("entity_id", entityId);
        command.Parameters.AddWithValue("before_json", beforeJson);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.Add(new NpgsqlParameter("changed_by_user_id", NpgsqlDbType.Uuid) { Value = changedByUserId ?? (object)DBNull.Value });
        command.Parameters.AddWithValue("changed_at_utc", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }

        return NpgsqlDataSource.Create(connectionString);
    }

    private static string QuoteIdentifier(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private sealed record PurgeDueRow(Guid Id, string Payload);

    private sealed class PurgeEntityResult
    {
        public int PurgedCount { get; set; }
        public int BlockedCount { get; set; }

        public void Add(AdminPurgeActionResult result)
        {
            if (result.Status == "Purged")
            {
                PurgedCount += 1;
            }
            else if (result.Status == "PurgeBlocked")
            {
                BlockedCount += 1;
            }
        }
    }
}

public sealed record AdminScheduledDeletionPurgeResult(
    int PurgedUserCount,
    int PurgedDepartmentCount,
    int PurgedHolidayCount,
    int BlockedCount);

public sealed record AdminPurgeActionResult(string Status, string Message);
