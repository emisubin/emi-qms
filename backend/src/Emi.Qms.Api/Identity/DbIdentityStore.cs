using Emi.Qms.Api.Authorization;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Identity;

public sealed class DbIdentityStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    IConfiguration configuration)
    : IIdentityStore
{
    public Task<UserAuthorizationProfile?> GetProfileByDevelopmentUserKeyAsync(
        string developmentUserKey,
        CancellationToken cancellationToken)
    {
        return GetProfileAsync(
            "u.development_user_key = @lookup and u.auth_provider = 'Dev'",
            [("lookup", developmentUserKey)],
            cancellationToken);
    }

    public Task<UserAuthorizationProfile?> GetProfileByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return GetProfileAsync(
            "u.id = @lookup",
            [("lookup", userId)],
            cancellationToken);
    }

    public Task<UserAuthorizationProfile?> GetProfileByEntraObjectIdAsync(
        string entraObjectId,
        CancellationToken cancellationToken)
    {
        return GetProfileAsync(
            "u.entra_object_id = @lookup and u.auth_provider = 'EntraId'",
            [("lookup", entraObjectId.Trim())],
            cancellationToken);
    }

    public Task<UserAuthorizationProfile?> GetDirectoryBoundEntraProfileAsync(
        Guid directoryUserId,
        string entraObjectId,
        CancellationToken cancellationToken)
    {
        if (directoryUserId == Guid.Empty || string.IsNullOrWhiteSpace(entraObjectId))
        {
            return Task.FromResult<UserAuthorizationProfile?>(null);
        }

        var normalizedObjectId = entraObjectId.Trim();
        return GetProfileAsync(
            """
            u.id = @directory_user_id
            and u.entra_object_id = @entra_object_id
            and u.development_user_key = @development_user_key
            and u.auth_provider = 'EntraId'
            and u.is_active = true
            """,
            [
                ("directory_user_id", directoryUserId),
                ("entra_object_id", normalizedObjectId),
                ("development_user_key", $"entra:{normalizedObjectId}")
            ],
            cancellationToken);
    }

    public async Task<UserAuthorizationProfile?> GetOrCreateEntraProfileAsync(
        string entraObjectId,
        string displayName,
        string? email,
        CancellationToken cancellationToken)
    {
        var normalizedObjectId = entraObjectId.Trim();
        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "Microsoft 365 사용자" : displayName.Trim();
        var normalizedEmail = NormalizeEmail(email);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = """
                select pg_advisory_xact_lock(hashtextextended(@entra_object_id, 0));
                """;
            lockCommand.Parameters.AddWithValue("entra_object_id", normalizedObjectId);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        Guid userId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into qms_users (
                    id,
                    development_user_key,
                    display_name,
                    department_id,
                    is_active,
                    entra_object_id,
                    email,
                    auth_provider
                )
                values (
                    @id,
                    @development_user_key,
                    @display_name,
                    null,
                    true,
                    @entra_object_id,
                    @email,
                    'EntraId'
                )
                on conflict (entra_object_id) where entra_object_id is not null do update
                set display_name = excluded.display_name,
                    email = excluded.email
                returning id;
                """;
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("development_user_key", $"entra:{normalizedObjectId}");
            command.Parameters.AddWithValue("display_name", normalizedDisplayName);
            command.Parameters.AddWithValue("entra_object_id", normalizedObjectId);
            AddNullableTextParameter(command, "email", normalizedEmail);

            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is not Guid id)
            {
                throw new InvalidOperationException("EntraId 사용자 생성 결과를 확인할 수 없습니다.");
            }

            userId = id;
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail)
            && BootstrapAdminEmails().Contains(normalizedEmail, StringComparer.Ordinal))
        {
            await AssignSystemAdministratorRoleAsync(connection, transaction, userId, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetProfileByUserIdAsync(userId, cancellationToken);
    }

    public async Task<UserAuthorizationProfile?> GetOrCreateDirectoryBoundEntraProfileAsync(
        Guid directoryUserId,
        string entraObjectId,
        string displayName,
        string? email,
        CancellationToken cancellationToken)
    {
        if (directoryUserId == Guid.Empty || string.IsNullOrWhiteSpace(entraObjectId))
        {
            return null;
        }

        var normalizedObjectId = entraObjectId.Trim();
        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? "Microsoft 365 사용자"
            : displayName.Trim();
        var normalizedEmail = NormalizeEmail(email);
        var developmentUserKey = $"entra:{normalizedObjectId}";

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var lockCommand = connection.CreateCommand())
            {
                lockCommand.Transaction = transaction;
                lockCommand.CommandText = """
                    select pg_advisory_xact_lock(hashtextextended(@entra_object_id, 0));
                    """;
                lockCommand.Parameters.AddWithValue("entra_object_id", normalizedObjectId);
                await lockCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            var candidates = new List<(
                Guid UserId,
                string DevelopmentUserKey,
                string? EntraObjectId,
                string AuthProvider,
                bool IsActive)>();
            await using (var collisionCommand = connection.CreateCommand())
            {
                collisionCommand.Transaction = transaction;
                collisionCommand.CommandText = """
                    select id, development_user_key, entra_object_id, auth_provider, is_active
                    from qms_users
                    where id = @directory_user_id
                       or entra_object_id = @entra_object_id
                       or development_user_key = @development_user_key
                    for update;
                    """;
                collisionCommand.Parameters.AddWithValue("directory_user_id", directoryUserId);
                collisionCommand.Parameters.AddWithValue("entra_object_id", normalizedObjectId);
                collisionCommand.Parameters.AddWithValue("development_user_key", developmentUserKey);
                await using var reader = await collisionCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    candidates.Add((
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.GetString(3),
                        reader.GetBoolean(4)));
                }
            }

            if (candidates.Count > 0)
            {
                if (candidates.Count != 1
                    || candidates[0].UserId != directoryUserId
                    || !string.Equals(
                        candidates[0].DevelopmentUserKey,
                        developmentUserKey,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        candidates[0].EntraObjectId,
                        normalizedObjectId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        candidates[0].AuthProvider,
                        QmsAuthProviders.EntraId,
                        StringComparison.Ordinal)
                    || !candidates[0].IsActive)
                {
                    return null;
                }

                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    update qms_users
                    set display_name = @display_name,
                        email = @email
                    where id = @directory_user_id;
                    """;
                update.Parameters.AddWithValue("display_name", normalizedDisplayName);
                AddNullableTextParameter(update, "email", normalizedEmail);
                update.Parameters.AddWithValue("directory_user_id", directoryUserId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    insert into qms_users (
                        id,
                        development_user_key,
                        display_name,
                        department_id,
                        is_active,
                        entra_object_id,
                        email,
                        auth_provider)
                    values (
                        @directory_user_id,
                        @development_user_key,
                        @display_name,
                        null,
                        true,
                        @entra_object_id,
                        @email,
                        'EntraId');
                    """;
                insert.Parameters.AddWithValue("directory_user_id", directoryUserId);
                insert.Parameters.AddWithValue("development_user_key", developmentUserKey);
                insert.Parameters.AddWithValue("display_name", normalizedDisplayName);
                insert.Parameters.AddWithValue("entra_object_id", normalizedObjectId);
                AddNullableTextParameter(insert, "email", normalizedEmail);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return null;
        }

        return await GetProfileByUserIdAsync(directoryUserId, cancellationToken);
    }

    public async Task<QmsProject?> GetProjectByKeyAsync(string projectKey, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, project_key, project_number, name
            from projects
            where project_key = @project_key;
            """);
        command.Parameters.AddWithValue("project_key", projectKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new QmsProject(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3))
            : null;
    }

    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var snapshot = await ReadUserAdministrationSnapshotAsync(cancellationToken);
        return snapshot.Users
            .Select(user => new UserSummary(
                user.DevelopmentUserKey,
                user.DisplayName,
                user.DepartmentCode ?? "",
                user.Roles,
                user.UserId,
                user.Email,
                user.AuthProvider,
                user.IsActive,
                user.ApprovalPending,
                user.DepartmentId?.ToString("D"),
                user.DepartmentName,
                user.IsReadOnly))
            .ToList();
    }

    public Task<UserAdministrationSnapshot> ReadUserAdministrationSnapshotAsync(CancellationToken cancellationToken)
    {
        return ReadUserAdministrationSnapshotAsync(includeDevUsers: false, cancellationToken);
    }

    public async Task<UserAdministrationSnapshot> ReadUserAdministrationSnapshotAsync(
        bool includeDevUsers,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var departments = await ReadDepartmentsAsync(connection, cancellationToken);
        var roles = await ReadRolesAsync(connection, cancellationToken);
        var users = await ReadUsersAsync(connection, includeDevUsers, cancellationToken);
        return new UserAdministrationSnapshot(users, departments, roles);
    }

    private async Task<UserAuthorizationProfile?> GetProfileAsync(
        string predicate,
        IReadOnlyList<(string Name, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var user = await ReadUserAsync(connection, predicate, parameters, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var department = await ReadDepartmentForUserAsync(connection, user.Id, cancellationToken);
        var roles = await ReadRolesForUserAsync(connection, user.Id, cancellationToken);
        var permissions = user.IsActive
            ? await ReadPermissionsForUserAsync(connection, user.Id, cancellationToken)
            : [];
        if (IsDevelopmentOperator(user, roles))
        {
            roles = await ReadRolesAsync(connection, cancellationToken);
            permissions = await ReadPermissionsAsync(connection, cancellationToken);
        }
        var projects = await ReadProjectAccessForUserAsync(connection, user.Id, cancellationToken);

        return OsanDepartmentPermissions.Apply(
            new UserAuthorizationProfile(user, department, roles, permissions, projects),
            connectionStringProvider.GetCurrentBusinessUnit()?.Code);
    }

    private static async Task<QmsUser?> ReadUserAsync(
        NpgsqlConnection connection,
        string predicate,
        IReadOnlyList<(string Name, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select u.id,
                   u.development_user_key,
                   u.display_name,
                   d.code,
                   u.is_active,
                   u.auth_provider,
                   u.email
            from qms_users u
            left join departments d on d.id = u.department_id
            where {predicate}
            limit 1;
            """;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new QmsUser(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6))
            : null;
    }

    private static async Task<Department?> ReadDepartmentForUserAsync(
        NpgsqlConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select d.id, d.code, d.name
            from qms_users u
            join departments d on d.id = u.department_id
            where u.id = @user_id;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new Department(reader.GetGuid(0), reader.GetString(1), reader.GetString(2))
            : null;
    }

    private static async Task<IReadOnlyList<Role>> ReadRolesForUserAsync(
        NpgsqlConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select r.id, r.code, r.name
            from roles r
            join user_roles ur on ur.role_id = r.id
            where ur.user_id = @user_id
            order by r.code;
            """;
        command.Parameters.AddWithValue("user_id", userId);

        var roles = new List<Role>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new Role(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return roles;
    }

    private static async Task<IReadOnlyList<Permission>> ReadPermissionsForUserAsync(
        NpgsqlConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select distinct p.id, p.code, p.name
            from permissions p
            join role_permissions rp on rp.permission_id = p.id
            join user_roles ur on ur.role_id = rp.role_id
            where ur.user_id = @user_id
            order by p.code;
            """;
        command.Parameters.AddWithValue("user_id", userId);

        var permissions = new List<Permission>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            permissions.Add(new Permission(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return permissions;
    }

    private static async Task<IReadOnlyList<Permission>> ReadPermissionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select id, code, name from permissions order by code;";
        var permissions = new List<Permission>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            permissions.Add(new Permission(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }
        return permissions;
    }

    private static async Task<IReadOnlyList<QmsProject>> ReadProjectAccessForUserAsync(
        NpgsqlConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select p.id, p.project_key, p.project_number, p.name
            from projects p
            join user_project_access upa on upa.project_id = p.id
            where upa.user_id = @user_id
            order by p.project_key;
            """;
        command.Parameters.AddWithValue("user_id", userId);

        var projects = new List<QmsProject>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(new QmsProject(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return projects;
    }

    private static async Task<IReadOnlyList<Department>> ReadDepartmentsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, code, name
            from departments
            where is_active = true
              and deletion_requested_at_utc is null
            order by code;
            """;
        var departments = new List<Department>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            departments.Add(new Department(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return departments;
    }

    private static async Task<IReadOnlyList<Role>> ReadRolesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select id, code, name from roles order by code;";
        var roles = new List<Role>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new Role(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return roles;
    }

    private static async Task<IReadOnlyList<UserAdministrationUser>> ReadUsersAsync(
        NpgsqlConnection connection,
        bool includeDevUsers,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select u.id,
                   u.development_user_key,
                   u.display_name,
                   u.email,
                   u.auth_provider,
                   u.is_active,
                   d.id,
                   d.code,
                   d.name,
                   u.deletion_requested_at_utc,
                   u.scheduled_hard_delete_at_utc,
                   u.purge_blocked_at_utc,
                   u.purge_blocked_reason,
                   u.pre_delete_is_active,
                   u.is_department_head,
                   coalesce(array_remove(array_agg(r.code order by r.code), null), array[]::text[]) as role_codes
            from qms_users u
            left join departments d on d.id = u.department_id
            left join user_roles ur on ur.user_id = u.id
            left join roles r on r.id = ur.role_id
            where @include_dev_users or u.auth_provider = 'EntraId'
            group by u.id, d.id, d.code, d.name
            order by u.auth_provider, u.display_name;
            """;
        command.Parameters.AddWithValue("include_dev_users", includeDevUsers);

        var users = new List<UserAdministrationUser>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var roles = reader.GetFieldValue<string[]>(15);
            var authProvider = reader.GetString(4);
            var isActive = reader.GetBoolean(5);
            users.Add(new UserAdministrationUser(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                authProvider,
                isActive,
                ApprovalReadinessPolicy.IsApprovalPending(
                    authProvider,
                    isActive,
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    roles),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                roles,
                authProvider != QmsAuthProviders.EntraId,
                reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
                reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
                reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetBoolean(13),
                reader.GetBoolean(14)));
        }

        return users;
    }

    private async Task AssignSystemAdministratorRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into user_roles (user_id, role_id)
            select @user_id, roles.id
            from roles
            where roles.code = @role_code
            on conflict do nothing;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role_code", QmsRoles.SystemAdministrator);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private IReadOnlyList<string> BootstrapAdminEmails()
    {
        var configured = configuration["AUTHENTICATION_BOOTSTRAP_ADMIN_EMAILS"]
            ?? configuration["Authentication:BootstrapAdminEmails"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return [];
        }

        return configured
            .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeEmail)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private bool IsDevelopmentOperator(QmsUser user, IReadOnlyList<Role> assignedRoles)
    {
        if (!user.IsActive
            || !string.Equals(user.AuthProvider, QmsAuthProviders.EntraId, StringComparison.Ordinal)
            || assignedRoles.Count == 0)
        {
            return false;
        }
        var email = NormalizeEmail(user.Email);
        if (email is null)
        {
            return false;
        }
        var configured = configuration["AUTHENTICATION_DEVELOPMENT_OPERATOR_EMAILS"]
            ?? configuration["Authentication:DevelopmentOperatorEmails"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }
        return configured
            .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeEmail)
            .Any(candidate => string.Equals(candidate, email, StringComparison.Ordinal));
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

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    private static void AddNullableTextParameter(NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value
        });
    }
}
