using System.Text.Json;
using Emi.Qms.Api.Admin;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Identity;

public sealed class UserAdministrationStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    DbIdentityStore dbIdentityStore,
    TimeProvider timeProvider)
    : IUserAdministrationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UserAdministrationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var developmentUsers = BuildDevelopmentUsers();
        if (string.IsNullOrWhiteSpace(connectionStringProvider.GetConnectionString()))
        {
            return new UserAdministrationSnapshot(
                developmentUsers,
                SeedIdentityData.Departments,
                SeedIdentityData.Roles);
        }

        var dbSnapshot = await dbIdentityStore.ReadUserAdministrationSnapshotAsync(cancellationToken);
        return dbSnapshot with
        {
            Users = developmentUsers.Concat(dbSnapshot.Users)
                .OrderBy(user => user.AuthProvider, StringComparer.Ordinal)
                .ThenBy(user => user.DisplayName, StringComparer.Ordinal)
                .ToList()
        };
    }

    public async Task<UserAdministrationMutationResult> UpdateEntraUserAsync(
        Guid userId,
        UpdateUserAdministrationRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var user = await ReadMutableUserAsync(connection, transaction, userId, cancellationToken);
        if (user is null)
        {
            return UserAdministrationMutationResult.Failure("사용자를 찾을 수 없습니다.");
        }

        if (!string.Equals(user.AuthProvider, QmsAuthProviders.EntraId, StringComparison.Ordinal))
        {
            return UserAdministrationMutationResult.Failure("개발 사용자는 이 화면에서 수정할 수 없습니다.");
        }

        var requestedRoleCodes = request.RoleCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        if (request.DepartmentId is not null
            && !await DepartmentExistsAsync(connection, transaction, request.DepartmentId.Value, cancellationToken))
        {
            return UserAdministrationMutationResult.Failure("존재하지 않는 부서입니다.");
        }

        if (!await RolesExistAsync(connection, transaction, requestedRoleCodes, cancellationToken))
        {
            return UserAdministrationMutationResult.Failure("존재하지 않는 역할이 포함되어 있습니다.");
        }

        var willBeAdmin = request.IsActive && requestedRoleCodes.Contains(QmsRoles.SystemAdministrator, StringComparer.Ordinal);
        if (!willBeAdmin)
        {
            var guardResult = await ActiveSystemAdministratorInvariantGuard.CheckRemovalAsync(
                connection,
                transaction,
                userId,
                cancellationToken);
            if (guardResult == ActiveSystemAdministratorGuardResult.Rejected)
            {
                return UserAdministrationMutationResult.Failure(
                    ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage);
            }
        }

        await using (var updateUser = connection.CreateCommand())
        {
            updateUser.Transaction = transaction;
            updateUser.CommandText = """
                update qms_users
                set department_id = @department_id,
                    is_active = @is_active,
                    deletion_requested_at_utc = @deletion_requested_at_utc,
                    scheduled_hard_delete_at_utc = @scheduled_hard_delete_at_utc,
                    purge_blocked_at_utc = null,
                    purge_blocked_reason = null,
                    pre_delete_is_active = @pre_delete_is_active
                where id = @user_id;
                """;
            updateUser.Parameters.AddWithValue("user_id", userId);
            updateUser.Parameters.AddWithValue("is_active", request.IsActive);
            updateUser.Parameters.Add(new NpgsqlParameter("deletion_requested_at_utc", NpgsqlDbType.TimestampTz)
            {
                Value = request.IsActive ? DBNull.Value : user.DeletionRequestedAtUtc ?? (object)DBNull.Value
            });
            updateUser.Parameters.Add(new NpgsqlParameter("scheduled_hard_delete_at_utc", NpgsqlDbType.TimestampTz)
            {
                Value = request.IsActive ? DBNull.Value : user.ScheduledHardDeleteAtUtc ?? (object)DBNull.Value
            });
            updateUser.Parameters.Add(new NpgsqlParameter("pre_delete_is_active", NpgsqlDbType.Boolean)
            {
                Value = request.IsActive ? DBNull.Value : user.PreDeleteIsActive ?? (object)DBNull.Value
            });
            updateUser.Parameters.Add(new NpgsqlParameter("department_id", NpgsqlDbType.Uuid)
            {
                Value = request.DepartmentId is null ? DBNull.Value : request.DepartmentId.Value
            });
            await updateUser.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteRoles = connection.CreateCommand())
        {
            deleteRoles.Transaction = transaction;
            deleteRoles.CommandText = "delete from user_roles where user_id = @user_id;";
            deleteRoles.Parameters.AddWithValue("user_id", userId);
            await deleteRoles.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var roleCode in requestedRoleCodes)
        {
            await using var insertRole = connection.CreateCommand();
            insertRole.Transaction = transaction;
            insertRole.CommandText = """
                insert into user_roles (user_id, role_id)
                select @user_id, roles.id
                from roles
                where roles.code = @role_code
                on conflict do nothing;
                """;
            insertRole.Parameters.AddWithValue("user_id", userId);
            insertRole.Parameters.AddWithValue("role_code", roleCode);
            await insertRole.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return UserAdministrationMutationResult.Success(await GetSnapshotAsync(cancellationToken));
    }

    public async Task<UserAdministrationMutationResult> ScheduleEntraUserDeletionAsync(
        Guid userId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var user = await ReadMutableUserAsync(connection, transaction, userId, cancellationToken);
        if (user is null)
        {
            return UserAdministrationMutationResult.Failure("사용자를 찾을 수 없습니다.");
        }

        if (!string.Equals(user.AuthProvider, QmsAuthProviders.EntraId, StringComparison.Ordinal))
        {
            return UserAdministrationMutationResult.Failure("개발 사용자는 삭제할 수 없습니다.");
        }

        var guardResult = await ActiveSystemAdministratorInvariantGuard.CheckRemovalAsync(
            connection,
            transaction,
            userId,
            cancellationToken);
        if (guardResult == ActiveSystemAdministratorGuardResult.Rejected)
        {
            return UserAdministrationMutationResult.Failure(
                ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage);
        }

        var now = timeProvider.GetUtcNow();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update qms_users
                set is_active = false,
                    pre_delete_is_active = coalesce(pre_delete_is_active, is_active),
                    deletion_requested_at_utc = @deletion_requested_at_utc,
                    scheduled_hard_delete_at_utc = @scheduled_hard_delete_at_utc,
                    purge_blocked_at_utc = null,
                    purge_blocked_reason = null
                where id = @user_id;
                """;
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("deletion_requested_at_utc", now);
            command.Parameters.AddWithValue("scheduled_hard_delete_at_utc", now.AddDays(7));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var after = await ReadMutableUserAsync(connection, transaction, userId, cancellationToken);
        await InsertChangeLogAsync(connection, transaction, "User", userId, "DeleteScheduled", user, after, "삭제", currentUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserAdministrationMutationResult.Success(await GetSnapshotAsync(cancellationToken));
    }

    public async Task<UserAdministrationMutationResult> RestoreEntraUserAsync(
        Guid userId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var user = await ReadMutableUserAsync(connection, transaction, userId, cancellationToken);
        if (user is null)
        {
            return UserAdministrationMutationResult.Failure("사용자를 찾을 수 없습니다.");
        }

        if (!string.Equals(user.AuthProvider, QmsAuthProviders.EntraId, StringComparison.Ordinal))
        {
            return UserAdministrationMutationResult.Failure("개발 사용자는 복구할 수 없습니다.");
        }

        if (user.DeletionRequestedAtUtc is null && user.PurgeBlockedAtUtc is null)
        {
            return UserAdministrationMutationResult.Failure("삭제 예정 또는 삭제 보류 사용자만 복구할 수 있습니다.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update qms_users
                set is_active = coalesce(pre_delete_is_active, true),
                    deletion_requested_at_utc = null,
                    scheduled_hard_delete_at_utc = null,
                    purge_blocked_at_utc = null,
                    purge_blocked_reason = null,
                    pre_delete_is_active = null
                where id = @user_id;
                """;
            command.Parameters.AddWithValue("user_id", userId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var after = await ReadMutableUserAsync(connection, transaction, userId, cancellationToken);
        await InsertChangeLogAsync(connection, transaction, "User", userId, "Restored", user, after, "삭제 예정 복구", currentUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserAdministrationMutationResult.Success(await GetSnapshotAsync(cancellationToken));
    }

    public async Task<AdminBulkActionResponse> BulkDeleteUsersAsync(
        IReadOnlyList<Guid> userIds,
        Guid currentUserId,
        AdminScheduledDeletionService deletionService,
        CancellationToken cancellationToken)
    {
        var ids = NormalizeIds(userIds);
        var items = new List<AdminBulkActionItemResponse>();
        foreach (var userId in ids)
        {
            var user = await ReadUserForBulkAsync(userId, cancellationToken);
            if (user is null)
            {
                items.Add(new AdminBulkActionItemResponse(userId, "Failed", "사용자를 찾을 수 없습니다."));
                continue;
            }

            if (user.DeletionRequestedAtUtc is not null || user.PurgeBlockedAtUtc is not null)
            {
                var purge = await deletionService.PurgeUserNowAsync(userId, currentUserId, cancellationToken);
                items.Add(new AdminBulkActionItemResponse(userId, purge.Status, purge.Message));
                continue;
            }

            var result = await ScheduleEntraUserDeletionAsync(userId, currentUserId, cancellationToken);
            items.Add(result.Succeeded
                ? new AdminBulkActionItemResponse(userId, "DeleteScheduled", "삭제 예정으로 처리했습니다.")
                : new AdminBulkActionItemResponse(userId, "Failed", result.ErrorMessage ?? "사용자를 삭제 예약할 수 없습니다."));
        }

        return BuildBulkResponse(ids.Count, items);
    }

    public async Task<AdminBulkActionResponse> BulkRestoreUsersAsync(
        IReadOnlyList<Guid> userIds,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var ids = NormalizeIds(userIds);
        var items = new List<AdminBulkActionItemResponse>();
        foreach (var userId in ids)
        {
            var user = await ReadUserForBulkAsync(userId, cancellationToken);
            if (user is null)
            {
                items.Add(new AdminBulkActionItemResponse(userId, "Failed", "사용자를 찾을 수 없습니다."));
                continue;
            }

            if (user.DeletionRequestedAtUtc is null && user.PurgeBlockedAtUtc is null)
            {
                items.Add(new AdminBulkActionItemResponse(userId, "Skipped", "삭제 예정 또는 삭제 보류 상태가 아니라 건너뛰었습니다."));
                continue;
            }

            var result = await RestoreEntraUserAsync(userId, currentUserId, cancellationToken);
            items.Add(result.Succeeded
                ? new AdminBulkActionItemResponse(userId, "Restored", "복구했습니다.")
                : new AdminBulkActionItemResponse(userId, "Failed", result.ErrorMessage ?? "사용자를 복구할 수 없습니다."));
        }

        return BuildBulkResponse(ids.Count, items);
    }

    private static IReadOnlyList<UserAdministrationUser> BuildDevelopmentUsers()
    {
        return SeedIdentityData.Users
            .OrderBy(user => user.DevelopmentUserKey, StringComparer.Ordinal)
            .Select(user =>
            {
                SeedIdentityData.UserRoles.TryGetValue(user.DevelopmentUserKey, out var roles);
                var department = SeedIdentityData.Departments.FirstOrDefault(item =>
                    string.Equals(item.Code, user.DepartmentCode, StringComparison.Ordinal));
                return new UserAdministrationUser(
                    user.Id,
                    user.DevelopmentUserKey,
                    user.DisplayName,
                    user.Email,
                    QmsAuthProviders.Dev,
                    user.IsActive,
                    ApprovalPending: false,
                    DepartmentId: department?.Id,
                    DepartmentCode: department?.Code,
                    DepartmentName: department?.Name,
                    Roles: roles ?? [],
                    IsReadOnly: true,
                    DeletionRequestedAtUtc: null,
                    ScheduledHardDeleteAtUtc: null,
                    PurgeBlockedAtUtc: null,
                    PurgeBlockedReason: null,
                    PreDeleteIsActive: null);
            })
            .ToList();
    }

    private static async Task<UserAdministrationUser?> ReadMutableUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id,
                   development_user_key,
                   display_name,
                   email,
                   auth_provider,
                   is_active,
                   deletion_requested_at_utc,
                   scheduled_hard_delete_at_utc,
                   purge_blocked_at_utc,
                   purge_blocked_reason,
                   pre_delete_is_active
            from qms_users
            where id = @user_id
            for update;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new UserAdministrationUser(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                ApprovalPending: false,
                DepartmentId: null,
                DepartmentCode: null,
                DepartmentName: null,
                Roles: [],
                IsReadOnly: false,
                DeletionRequestedAtUtc: reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
                ScheduledHardDeleteAtUtc: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                PurgeBlockedAtUtc: reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                PurgeBlockedReason: reader.IsDBNull(9) ? null : reader.GetString(9),
                PreDeleteIsActive: reader.IsDBNull(10) ? null : reader.GetBoolean(10))
            : null;
    }

    private async Task<UserAdministrationUser?> ReadUserForBulkAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return await ReadMutableUserAsync(connection, transaction, userId, cancellationToken);
    }

    private static async Task<bool> DepartmentExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
                select 1
                from departments
                where id = @department_id
                  and is_active = true
                  and deletion_requested_at_utc is null
            );
            """;
        command.Parameters.AddWithValue("department_id", departmentId);
        return await command.ExecuteScalarAsync(cancellationToken) is bool exists && exists;
    }

    private static async Task<bool> RolesExistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<string> roleCodes,
        CancellationToken cancellationToken)
    {
        if (roleCodes.Count == 0)
        {
            return true;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select count(*) from roles where code = any(@role_codes);";
        command.Parameters.AddWithValue("role_codes", roleCodes.ToArray());
        return await command.ExecuteScalarAsync(cancellationToken) is long count && count == roleCodes.Count;
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

    private static async Task InsertChangeLogAsync(
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
        command.Parameters.Add(new NpgsqlParameter("reason", NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(reason) ? DBNull.Value : reason.Trim() });
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
}
