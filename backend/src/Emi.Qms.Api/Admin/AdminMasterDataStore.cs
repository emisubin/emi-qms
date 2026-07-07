using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Admin;

public sealed class AdminMasterDataStore(DatabaseConnectionStringProvider connectionStringProvider, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                (
                    select count(*)::integer
                    from qms_users u
                    where u.auth_provider = 'EntraId'
                      and u.is_active = true
                      and not exists (select 1 from user_roles ur where ur.user_id = u.id)
                ) as pending_user_count,
                (
                    select count(*)::integer
                    from notification_deliveries
                    where status = 'Failed'
                ) as failed_delivery_count,
                (
                    select count(*)::integer
                    from notification_deliveries
                    where status = 'Pending'
                ) as pending_delivery_count,
                (
                    select max(sent_at_utc)
                    from notification_deliveries
                    where delivery_type = 'DailyDigest'
                      and status in ('Sent', 'DryRunSent')
                ) as last_digest_sent_at_utc,
                (
                    select count(*)::integer
                    from work_item_escalations
                    where status = 'Active'
                ) as active_escalation_count,
                (
                    select count(*)::integer
                    from admin_master_change_logs
                    where changed_at_utc >= now() - interval '7 days'
                ) as recent_master_change_count;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new AdminDashboardResponse(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }

    public async Task<AdminDepartmentListResponse> ListDepartmentsAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select d.id,
                   d.code,
                   d.name,
                   d.is_active,
                   d.sort_order,
                   count(u.id)::integer as user_count,
                   d.updated_at_utc,
                   d.deletion_requested_at_utc,
                   d.scheduled_hard_delete_at_utc,
                   d.purge_blocked_at_utc,
                   d.purge_blocked_reason,
                   d.pre_delete_is_active
            from departments d
            left join qms_users u on u.department_id = d.id
            group by d.id, d.code, d.name, d.is_active, d.sort_order, d.updated_at_utc,
                     d.deletion_requested_at_utc, d.scheduled_hard_delete_at_utc,
                     d.purge_blocked_at_utc, d.purge_blocked_reason, d.pre_delete_is_active
            order by d.sort_order, d.name;
            """);

        var departments = new List<AdminDepartmentMasterResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            departments.Add(ReadDepartment(reader));
        }

        return new AdminDepartmentListResponse(departments);
    }

    public async Task<AdminMasterMutationResult<AdminDepartmentMasterResponse>> CreateDepartmentAsync(
        CreateAdminDepartmentRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        var code = NormalizeCode(request.Code);
        var name = NormalizeRequired(request.Name);
        var fieldErrors = ValidateDepartmentCreate(request, code, name);
        if (fieldErrors.Count > 0)
        {
            return AdminMasterMutationResult<AdminDepartmentMasterResponse>.ValidationFailure(fieldErrors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var id = Guid.NewGuid();

        try
        {
            if (await DepartmentCodeExistsAsync(connection, transaction, code!, null, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return AdminMasterMutationResult<AdminDepartmentMasterResponse>.ValidationFailure(
                    new Dictionary<string, IReadOnlyList<string>>
                    {
                        ["code"] = ["이미 사용 중인 부서 코드입니다."]
                    });
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into departments (id, code, name, is_active, sort_order, updated_at_utc)
                values (@id, @code, @name, @is_active, @sort_order, now());
                """;
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("code", code!);
            command.Parameters.AddWithValue("name", name!);
            command.Parameters.AddWithValue("is_active", request.IsActive ?? true);
            command.Parameters.AddWithValue("sort_order", request.SortOrder ?? 1000);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AdminMasterMutationResult<AdminDepartmentMasterResponse>.ValidationFailure(
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["code"] = ["이미 사용 중인 부서 코드입니다."]
                });
        }

        var after = await ReadDepartmentByIdAsync(connection, transaction, id, forUpdate: false, cancellationToken);
        await InsertChangeLogAsync(connection, transaction, "Department", id, "Create", null, after, request.Reason, changedByUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMasterMutationResult<AdminDepartmentMasterResponse>.Success(after!);
    }

    public async Task<AdminMasterMutationResult<AdminDepartmentMasterResponse>> UpdateDepartmentAsync(
        Guid departmentId,
        UpdateAdminDepartmentRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        var name = NormalizeRequired(request.Name);
        var fieldErrors = ValidateDepartmentUpdate(request, name);
        if (fieldErrors.Count > 0)
        {
            return AdminMasterMutationResult<AdminDepartmentMasterResponse>.ValidationFailure(fieldErrors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var before = await ReadDepartmentByIdAsync(connection, transaction, departmentId, forUpdate: true, cancellationToken);
        if (before is null)
        {
            return AdminMasterMutationResult<AdminDepartmentMasterResponse>.Failure("부서를 찾을 수 없습니다.");
        }

        var nextIsActive = request.IsActive ?? before.IsActive;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update departments
                set name = @name,
                    is_active = @is_active,
                    sort_order = @sort_order,
                    updated_at_utc = now(),
                    deletion_requested_at_utc = @deletion_requested_at_utc,
                    scheduled_hard_delete_at_utc = @scheduled_hard_delete_at_utc,
                    purge_blocked_at_utc = null,
                    purge_blocked_reason = null,
                    pre_delete_is_active = @pre_delete_is_active
                where id = @id;
                """;
            command.Parameters.AddWithValue("id", departmentId);
            command.Parameters.AddWithValue("name", name!);
            command.Parameters.AddWithValue("is_active", nextIsActive);
            command.Parameters.AddWithValue("sort_order", request.SortOrder ?? before.SortOrder);
            command.Parameters.Add(new NpgsqlParameter("deletion_requested_at_utc", NpgsqlDbType.TimestampTz)
            {
                Value = nextIsActive ? DBNull.Value : before.DeletionRequestedAtUtc ?? (object)DBNull.Value
            });
            command.Parameters.Add(new NpgsqlParameter("scheduled_hard_delete_at_utc", NpgsqlDbType.TimestampTz)
            {
                Value = nextIsActive ? DBNull.Value : before.ScheduledHardDeleteAtUtc ?? (object)DBNull.Value
            });
            command.Parameters.Add(new NpgsqlParameter("pre_delete_is_active", NpgsqlDbType.Boolean)
            {
                Value = nextIsActive ? DBNull.Value : before.PreDeleteIsActive ?? (object)DBNull.Value
            });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var after = await ReadDepartmentByIdAsync(connection, transaction, departmentId, forUpdate: false, cancellationToken);
        await InsertChangeLogAsync(connection, transaction, "Department", departmentId, "Update", before, after, request.Reason, changedByUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMasterMutationResult<AdminDepartmentMasterResponse>.Success(after!);
    }

    public async Task<AdminMasterMutationResult<AdminDepartmentMasterResponse>> ScheduleDepartmentDeletionAsync(
        Guid departmentId,
        string? reason,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var before = await ReadDepartmentByIdAsync(connection, transaction, departmentId, forUpdate: true, cancellationToken);
        if (before is null)
        {
            return AdminMasterMutationResult<AdminDepartmentMasterResponse>.Failure("부서를 찾을 수 없습니다.");
        }

        var now = timeProvider.GetUtcNow();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update departments
                set is_active = false,
                    pre_delete_is_active = coalesce(pre_delete_is_active, is_active),
                    updated_at_utc = @updated_at_utc,
                    deletion_requested_at_utc = @deletion_requested_at_utc,
                    scheduled_hard_delete_at_utc = @scheduled_hard_delete_at_utc,
                    purge_blocked_at_utc = null,
                    purge_blocked_reason = null
                where id = @id;
                """;
            command.Parameters.AddWithValue("id", departmentId);
            command.Parameters.AddWithValue("updated_at_utc", now);
            command.Parameters.AddWithValue("deletion_requested_at_utc", now);
            command.Parameters.AddWithValue("scheduled_hard_delete_at_utc", now.AddDays(7));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var after = await ReadDepartmentByIdAsync(connection, transaction, departmentId, forUpdate: false, cancellationToken);
        await InsertChangeLogAsync(connection, transaction, "Department", departmentId, "DeleteScheduled", before, after, reason, changedByUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMasterMutationResult<AdminDepartmentMasterResponse>.Success(after!);
    }

    public async Task<AdminMasterMutationResult<AdminDepartmentMasterResponse>> RestoreDepartmentAsync(
        Guid departmentId,
        string? reason,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var before = await ReadDepartmentByIdAsync(connection, transaction, departmentId, forUpdate: true, cancellationToken);
        if (before is null)
        {
            return AdminMasterMutationResult<AdminDepartmentMasterResponse>.Failure("부서를 찾을 수 없습니다.");
        }

        if (before.DeletionRequestedAtUtc is null && before.PurgeBlockedAtUtc is null)
        {
            return AdminMasterMutationResult<AdminDepartmentMasterResponse>.Failure("삭제 예정 또는 삭제 보류 부서만 복구할 수 있습니다.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update departments
                set is_active = coalesce(pre_delete_is_active, true),
                    updated_at_utc = now(),
                    deletion_requested_at_utc = null,
                    scheduled_hard_delete_at_utc = null,
                    purge_blocked_at_utc = null,
                    purge_blocked_reason = null,
                    pre_delete_is_active = null
                where id = @id;
                """;
            command.Parameters.AddWithValue("id", departmentId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var after = await ReadDepartmentByIdAsync(connection, transaction, departmentId, forUpdate: false, cancellationToken);
        await InsertChangeLogAsync(connection, transaction, "Department", departmentId, "Restored", before, after, reason, changedByUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMasterMutationResult<AdminDepartmentMasterResponse>.Success(after!);
    }

    public async Task<AdminBulkActionResponse> BulkDeleteDepartmentsAsync(
        IReadOnlyList<Guid> departmentIds,
        Guid changedByUserId,
        AdminScheduledDeletionService deletionService,
        CancellationToken cancellationToken)
    {
        var ids = NormalizeIds(departmentIds);
        var items = new List<AdminBulkActionItemResponse>();
        foreach (var departmentId in ids)
        {
            var department = await ReadDepartmentForBulkAsync(departmentId, cancellationToken);
            if (department is null)
            {
                items.Add(new AdminBulkActionItemResponse(departmentId, "Failed", "부서를 찾을 수 없습니다."));
                continue;
            }

            if (department.DeletionRequestedAtUtc is not null || department.PurgeBlockedAtUtc is not null)
            {
                var purge = await deletionService.PurgeDepartmentNowAsync(departmentId, changedByUserId, cancellationToken);
                items.Add(new AdminBulkActionItemResponse(departmentId, purge.Status, purge.Message));
                continue;
            }

            var result = await ScheduleDepartmentDeletionAsync(departmentId, "일괄 삭제", changedByUserId, cancellationToken);
            items.Add(result.Succeeded
                ? new AdminBulkActionItemResponse(departmentId, "DeleteScheduled", "삭제 예정으로 처리했습니다.")
                : new AdminBulkActionItemResponse(departmentId, "Failed", result.ErrorMessage ?? "부서를 삭제 예약할 수 없습니다."));
        }

        return BuildBulkResponse(ids.Count, items);
    }

    public async Task<AdminBulkActionResponse> BulkRestoreDepartmentsAsync(
        IReadOnlyList<Guid> departmentIds,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        var ids = NormalizeIds(departmentIds);
        var items = new List<AdminBulkActionItemResponse>();
        foreach (var departmentId in ids)
        {
            var department = await ReadDepartmentForBulkAsync(departmentId, cancellationToken);
            if (department is null)
            {
                items.Add(new AdminBulkActionItemResponse(departmentId, "Failed", "부서를 찾을 수 없습니다."));
                continue;
            }

            if (department.DeletionRequestedAtUtc is null && department.PurgeBlockedAtUtc is null)
            {
                items.Add(new AdminBulkActionItemResponse(departmentId, "Skipped", "삭제 예정 또는 삭제 보류 상태가 아니라 건너뛰었습니다."));
                continue;
            }

            var result = await RestoreDepartmentAsync(departmentId, "일괄 복구", changedByUserId, cancellationToken);
            items.Add(result.Succeeded
                ? new AdminBulkActionItemResponse(departmentId, "Restored", "복구했습니다.")
                : new AdminBulkActionItemResponse(departmentId, "Failed", result.ErrorMessage ?? "부서를 복구할 수 없습니다."));
        }

        return BuildBulkResponse(ids.Count, items);
    }

    public async Task<AdminDepartmentListResponse> ReorderDepartmentsAsync(
        AdminReorderRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var item in request.Items)
        {
            if (item.SortOrder < 0)
            {
                continue;
            }

            var before = await ReadDepartmentByIdAsync(connection, transaction, item.Id, forUpdate: true, cancellationToken);
            if (before is null)
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                update departments
                set sort_order = @sort_order,
                    updated_at_utc = now()
                where id = @id;
                """;
            command.Parameters.AddWithValue("id", item.Id);
            command.Parameters.AddWithValue("sort_order", item.SortOrder);
            await command.ExecuteNonQueryAsync(cancellationToken);
            var after = await ReadDepartmentByIdAsync(connection, transaction, item.Id, forUpdate: false, cancellationToken);
            await InsertChangeLogAsync(connection, transaction, "Department", item.Id, "Reorder", before, after, request.Reason, changedByUserId, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await ListDepartmentsAsync(cancellationToken);
    }

    public async Task<PermissionMatrixResponse> GetPermissionMatrixAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var roles = new List<PermissionMatrixRoleResponse>();
        var permissions = new List<PermissionMatrixPermissionResponse>();
        var assignments = new List<PermissionMatrixAssignmentResponse>();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select id, code, name from roles order by code;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(new PermissionMatrixRoleResponse(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select id, code, name from permissions order by code;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                permissions.Add(new PermissionMatrixPermissionResponse(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select role_id, permission_id from role_permissions;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                assignments.Add(new PermissionMatrixAssignmentResponse(reader.GetGuid(0), reader.GetGuid(1)));
            }
        }

        return new PermissionMatrixResponse(roles, permissions, assignments);
    }

    public async Task<AdminMasterChangeLogListResponse> ListChangeLogsAsync(
        string? entityType,
        Guid? changedByUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select logs.id,
                   logs.entity_type,
                   logs.entity_id,
                   logs.action,
                   logs.before_json,
                   logs.after_json,
                   logs.reason,
                   logs.changed_by_user_id,
                   users.display_name,
                   logs.changed_at_utc
            from admin_master_change_logs logs
            left join qms_users users on users.id = logs.changed_by_user_id
            where (@entity_type is null or logs.entity_type = @entity_type)
              and (@changed_by_user_id is null or logs.changed_by_user_id = @changed_by_user_id)
              and (@from_utc is null or logs.changed_at_utc >= @from_utc)
              and (@to_utc is null or logs.changed_at_utc <= @to_utc)
            order by logs.changed_at_utc desc
            limit 200;
            """);
        command.Parameters.Add(new NpgsqlParameter("entity_type", NpgsqlDbType.Text) { Value = NormalizeOptional(entityType) ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("changed_by_user_id", NpgsqlDbType.Uuid) { Value = changedByUserId ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("from_utc", NpgsqlDbType.TimestampTz) { Value = from ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("to_utc", NpgsqlDbType.TimestampTz) { Value = to ?? (object)DBNull.Value });

        var items = new List<AdminMasterChangeLogResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AdminMasterChangeLogResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetGuid(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetFieldValue<DateTimeOffset>(9)));
        }

        return new AdminMasterChangeLogListResponse(items);
    }

    public async Task<AdminWorkItemHistoryListResponse> ListWorkItemHistoryAsync(
        Guid? projectId,
        Guid? userId,
        string? stage,
        string? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select wi.id,
                   wi.project_id,
                   p.project_title,
                   p.project_code,
                   wi.workflow_stage_code,
                   coalesce(ws.stage_name, wi.workflow_stage_code) as workflow_stage_name,
                   wi.title,
                   wi.status,
                   wi.assigned_user_id,
                   u.display_name,
                   wi.started_at_utc,
                   wi.completed_at_utc,
                   wi.cancelled_at_utc,
                   wi.due_date,
                   wi.created_at_utc,
                   wi.created_at_utc as updated_at_utc
            from work_items wi
            join projects p on p.id = wi.project_id
            left join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            left join qms_users u on u.id = wi.assigned_user_id
            where (@project_id is null or wi.project_id = @project_id)
              and (@user_id is null or wi.assigned_user_id = @user_id)
              and (@stage is null or wi.workflow_stage_code = @stage)
              and (@status is null or wi.status = @status)
              and (@from_utc is null or coalesce(wi.started_at_utc, wi.completed_at_utc, wi.cancelled_at_utc, wi.created_at_utc) >= @from_utc)
              and (@to_utc is null or coalesce(wi.started_at_utc, wi.completed_at_utc, wi.cancelled_at_utc, wi.created_at_utc) <= @to_utc)
            order by coalesce(wi.started_at_utc, wi.completed_at_utc, wi.cancelled_at_utc, wi.created_at_utc) desc
            limit 200;
            """);
        command.Parameters.Add(new NpgsqlParameter("project_id", NpgsqlDbType.Uuid) { Value = projectId ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("user_id", NpgsqlDbType.Uuid) { Value = userId ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("stage", NpgsqlDbType.Text) { Value = NormalizeOptional(stage) ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Text) { Value = NormalizeOptional(status) ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("from_utc", NpgsqlDbType.TimestampTz) { Value = from ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("to_utc", NpgsqlDbType.TimestampTz) { Value = to ?? (object)DBNull.Value });

        var items = new List<AdminWorkItemHistoryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AdminWorkItemHistoryResponse(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
                reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
                reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
                reader.IsDBNull(13) ? null : reader.GetFieldValue<DateOnly>(13),
                reader.GetFieldValue<DateTimeOffset>(14),
                reader.GetFieldValue<DateTimeOffset>(15)));
        }

        return new AdminWorkItemHistoryListResponse(items);
    }

    private async Task<AdminDepartmentMasterResponse?> ReadDepartmentByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid departmentId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select d.id,
                   d.code,
                   d.name,
                   d.is_active,
                   d.sort_order,
                   (
                       select count(*)::integer
                       from qms_users u
                       where u.department_id = d.id
                   ) as user_count,
                   d.updated_at_utc,
                   d.deletion_requested_at_utc,
                   d.scheduled_hard_delete_at_utc,
                   d.purge_blocked_at_utc,
                   d.purge_blocked_reason,
                   d.pre_delete_is_active
            from departments d
            where d.id = @id
            {(forUpdate ? "for update" : "")};
            """;
        command.Parameters.AddWithValue("id", departmentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDepartment(reader) : null;
    }

    private async Task<AdminDepartmentMasterResponse?> ReadDepartmentForBulkAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return await ReadDepartmentByIdAsync(connection, transaction, departmentId, forUpdate: false, cancellationToken);
    }

    private async Task InsertChangeLogAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string entityType,
        Guid? entityId,
        string action,
        object? before,
        object? after,
        string? reason,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into admin_master_change_logs (
                entity_type, entity_id, action, before_json, after_json, reason, changed_by_user_id
            )
            values (
                @entity_type, @entity_id, @action, @before_json, @after_json, @reason, @changed_by_user_id
            );
            """;
        command.Parameters.AddWithValue("entity_type", entityType);
        command.Parameters.Add(new NpgsqlParameter("entity_id", NpgsqlDbType.Uuid) { Value = entityId ?? (object)DBNull.Value });
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add(new NpgsqlParameter("before_json", NpgsqlDbType.Text) { Value = before is null ? DBNull.Value : JsonSerializer.Serialize(before, JsonOptions) });
        command.Parameters.Add(new NpgsqlParameter("after_json", NpgsqlDbType.Text) { Value = after is null ? DBNull.Value : JsonSerializer.Serialize(after, JsonOptions) });
        command.Parameters.Add(new NpgsqlParameter("reason", NpgsqlDbType.Text) { Value = NormalizeOptional(reason) ?? (object)DBNull.Value });
        command.Parameters.AddWithValue("changed_by_user_id", changedByUserId);
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

    private static AdminDepartmentMasterResponse ReadDepartment(NpgsqlDataReader reader)
    {
        return new AdminDepartmentMasterResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetBoolean(11));
    }

    private static Dictionary<string, IReadOnlyList<string>> ValidateDepartmentCreate(
        CreateAdminDepartmentRequest request,
        string? normalizedCode,
        string? normalizedName)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        var rawCode = request.Code?.Trim();

        if (string.IsNullOrWhiteSpace(rawCode))
        {
            errors["code"] = ["부서 코드는 필수입니다."];
        }
        else if (rawCode.Length is < 2 or > 50)
        {
            errors["code"] = ["부서 코드는 2~50자로 입력해주세요."];
        }
        else if (normalizedCode is null)
        {
            errors["code"] = ["부서 코드는 영문 대문자, 숫자, 하이픈(-), 언더스코어(_)만 사용할 수 있습니다."];
        }

        AddDepartmentNameErrors(errors, normalizedName);
        AddSortOrderErrors(errors, request.SortOrder);
        return errors;
    }

    private static Dictionary<string, IReadOnlyList<string>> ValidateDepartmentUpdate(
        UpdateAdminDepartmentRequest request,
        string? normalizedName)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        AddDepartmentNameErrors(errors, normalizedName);
        AddSortOrderErrors(errors, request.SortOrder);
        return errors;
    }

    private static void AddDepartmentNameErrors(
        IDictionary<string, IReadOnlyList<string>> errors,
        string? normalizedName)
    {
        if (normalizedName is null)
        {
            errors["name"] = ["부서명은 필수입니다."];
            return;
        }

        if (normalizedName.Length > 100)
        {
            errors["name"] = ["부서명은 100자 이하로 입력해주세요."];
            return;
        }

        if (!normalizedName.All(IsAllowedDepartmentNameCharacter))
        {
            errors["name"] = ["부서명에는 한글, 영문, 숫자, 공백, 괄호, 하이픈만 사용할 수 있습니다."];
        }
    }

    private static void AddSortOrderErrors(IDictionary<string, IReadOnlyList<string>> errors, int? sortOrder)
    {
        if (sortOrder is null)
        {
            return;
        }

        if (sortOrder is < 0 or > 9999)
        {
            errors["sortOrder"] = ["정렬 순서는 0 이상 9999 이하로 입력해주세요."];
        }
    }

    private static bool IsAllowedDepartmentNameCharacter(char character)
    {
        return character is >= '가' and <= '힣'
            || char.IsAsciiLetterOrDigit(character)
            || char.IsWhiteSpace(character)
            || character is '(' or ')' or '-';
    }

    private static async Task<bool> DepartmentCodeExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string code,
        Guid? excludingDepartmentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from departments
                where lower(code) = lower(@code)
                  and (@excluding_department_id is null or id <> @excluding_department_id)
            );
            """;
        command.Parameters.AddWithValue("code", code);
        command.Parameters.Add(new NpgsqlParameter("excluding_department_id", NpgsqlDbType.Uuid)
        {
            Value = excludingDepartmentId ?? (object)DBNull.Value
        });
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static IReadOnlyList<Guid> NormalizeIds(IReadOnlyList<Guid> ids)
    {
        return ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static AdminBulkActionResponse BuildBulkResponse(int requestedCount, IReadOnlyList<AdminBulkActionItemResponse> items)
    {
        return new AdminBulkActionResponse(
            requestedCount,
            items.Count(item => item.Status is "DeleteScheduled" or "Restored" or "Purged" or "PurgeBlocked"),
            items.Count(item => item.Status == "Failed"),
            items.Count(item => item.Status == "Skipped"),
            items);
    }

    private static string? NormalizeRequired(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeCode(string? value)
    {
        var code = NormalizeRequired(value)?.ToUpperInvariant();
        return code is not null && code.All(character => character is >= 'A' and <= 'Z' || char.IsDigit(character) || character is '-' or '_')
            ? code
            : null;
    }
}
