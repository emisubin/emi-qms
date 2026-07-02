using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Identity;

public sealed class UserAdministrationStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    DbIdentityStore dbIdentityStore)
    : IUserAdministrationStore
{
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

        var currentlyAdmin = await UserHasRoleAsync(connection, transaction, userId, QmsRoles.SystemAdministrator, cancellationToken);
        var willBeAdmin = request.IsActive && requestedRoleCodes.Contains(QmsRoles.SystemAdministrator, StringComparer.Ordinal);
        if (currentlyAdmin && !willBeAdmin)
        {
            var otherActiveAdminCount = await CountOtherActiveSystemAdministratorsAsync(
                connection,
                transaction,
                userId,
                cancellationToken);
            if (otherActiveAdminCount == 0)
            {
                return UserAdministrationMutationResult.Failure("마지막 active System Administrator는 비활성화하거나 역할을 제거할 수 없습니다.");
            }
        }

        await using (var updateUser = connection.CreateCommand())
        {
            updateUser.Transaction = transaction;
            updateUser.CommandText = """
                update qms_users
                set department_id = @department_id,
                    is_active = @is_active
                where id = @user_id;
                """;
            updateUser.Parameters.AddWithValue("user_id", userId);
            updateUser.Parameters.AddWithValue("is_active", request.IsActive);
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
                    IsReadOnly: true);
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
            select id, development_user_key, display_name, email, auth_provider, is_active
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
                IsReadOnly: false)
            : null;
    }

    private static async Task<bool> DepartmentExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from departments where id = @department_id);";
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

    private static async Task<bool> UserHasRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        string roleCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
                select 1
                from user_roles ur
                join roles r on r.id = ur.role_id
                where ur.user_id = @user_id
                  and r.code = @role_code
            );
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role_code", roleCode);
        return await command.ExecuteScalarAsync(cancellationToken) is bool exists && exists;
    }

    private static async Task<long> CountOtherActiveSystemAdministratorsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid excludedUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select count(*)
            from qms_users u
            join user_roles ur on ur.user_id = u.id
            join roles r on r.id = ur.role_id
            where u.is_active = true
              and u.auth_provider = 'EntraId'
              and r.code = @role_code
              and u.id <> @excluded_user_id;
            """;
        command.Parameters.AddWithValue("role_code", QmsRoles.SystemAdministrator);
        command.Parameters.AddWithValue("excluded_user_id", excludedUserId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
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
