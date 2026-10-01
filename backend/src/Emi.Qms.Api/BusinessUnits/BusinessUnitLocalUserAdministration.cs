using Emi.Qms.Api.Audit;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.BusinessUnits;

public sealed partial class BusinessUnitAccessAdministrationStore
{
    private interface ILocalUserAdministration
    {
        Task<PreparedProfile> PrepareProfileAsync(NormalizedProfile requested, bool isOverallAdministrator, CancellationToken cancellationToken);
        Task ApplyLocalProfileAsync(DirectoryIdentity directoryIdentity, PreparedProfile prepared, Guid actorUserId, Guid operationId, CancellationToken cancellationToken);
        Task<BusinessState> ReadBusinessStateAsync(IReadOnlyList<BusinessUnitAccessAdministrationUser> directoryUsers, CancellationToken cancellationToken);
    }

    // This local adapter exposes only user/department/role administration, with one fixed business DB.
    private sealed class LocalUserAdministration(BusinessDatabase database,
        MigrationLedgerInspector businessMigrationLedgerInspector, TimeProvider timeProvider) : ILocalUserAdministration
    {
        private BusinessUnitDatabaseTarget target => database.GetCurrentBusinessUnit();

    public async Task<PreparedProfile> PrepareProfileAsync(
        NormalizedProfile requested,
        bool isOverallAdministrator,
        CancellationToken cancellationToken)
    {
        await using var dataSource = database.RentDataSource(database.GetConnectionString() ?? throw new InvalidOperationException("Database connection is not configured."));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await ValidateBusinessContractAsync(connection, target, cancellationToken);

        if (!requested.IsActive && !isOverallAdministrator)
        {
            return new PreparedProfile(
                target.Code,
                requested.DepartmentId,
                null,
                requested.RoleCodes,
                false,
                false,
                false,
                null,
                false);
        }

        var departmentId = requested.DepartmentId;
        if (departmentId is null && isOverallAdministrator)
        {
            await using var defaultDepartment = connection.CreateCommand();
            defaultDepartment.CommandText = "select id from departments where code = 'administration' and is_active = true;";
            var defaultDepartmentId = await defaultDepartment.ExecuteScalarAsync(cancellationToken);
            departmentId = defaultDepartmentId is Guid value
                ? value
                : throw Error("overall_administrator_department_missing", StatusCodes.Status503ServiceUnavailable);
        }

        string departmentCode;
        await using (var departmentCommand = connection.CreateCommand())
        {
            departmentCommand.CommandText = """
                select department.code
                from departments department
                where department.id = @department_id
                  and department.is_active = true;
                """;
            departmentCommand.Parameters.AddWithValue("department_id", departmentId!.Value);
            await using var reader = await departmentCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw Error("department_not_found", StatusCodes.Status400BadRequest);
            }
            departmentCode = reader.GetString(0);
        }

        var roleCodes = requested.RoleCodes.ToList();
        if (isOverallAdministrator && !roleCodes.Contains(QmsRoles.SystemAdministrator, StringComparer.Ordinal))
        {
            roleCodes.Add(QmsRoles.SystemAdministrator);
        }
        var requiredDefault = DepartmentIdentityPolicy.GetDefaultRoleCode(departmentCode);
        if (requiredDefault is null)
        {
            throw Error("department_default_role_missing", StatusCodes.Status409Conflict);
        }
        if (!roleCodes.Contains(requiredDefault, StringComparer.Ordinal))
        {
            roleCodes.Add(requiredDefault);
            roleCodes.Sort(StringComparer.Ordinal);
        }

        await using var rolesCommand = connection.CreateCommand();
        rolesCommand.CommandText = "select count(*) from roles role where role.code = any(@role_codes);";
        rolesCommand.Parameters.AddWithValue("role_codes", NpgsqlDbType.Array | NpgsqlDbType.Text, roleCodes.ToArray());
        var roleCount = (long)(await rolesCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
        if (roleCount != roleCodes.Count)
        {
            throw Error("role_not_found", StatusCodes.Status400BadRequest);
        }

        return new PreparedProfile(
            target.Code,
            departmentId,
            departmentCode,
            roleCodes,
            true,
            requested.IsDepartmentHead,
            requested.IsDepartmentHeadConfirmed,
            requiredDefault,
            isOverallAdministrator);
    }

    public async Task ApplyLocalProfileAsync(
        DirectoryIdentity directoryIdentity,
        PreparedProfile prepared,
        Guid actorUserId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var parentAudit = AuditRequestContext.Current;
        using var auditScope = AuditRequestContext.Push(new AuditMutationContext(
            actorUserId,
            parentAudit?.ActualActorUserId,
            operationId,
            parentAudit?.LoginCorrelationId,
            "UserAccess",
            prepared.IsActive ? "ApproveOrUpdate" : "Revoke",
            "UpdateIntegratedUserAccess"));
        await using var dataSource = database.RentDataSource(database.GetConnectionString() ?? throw new InvalidOperationException("Database connection is not configured."));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "select pg_advisory_xact_lock(hashtextextended(@user_id::text, 0));";
            lockCommand.Parameters.AddWithValue("user_id", directoryIdentity.UserId);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var existing = await ReadExistingLocalProfileAsync(connection, transaction, directoryIdentity.UserId, cancellationToken);
        if (existing is not null
            && (!string.Equals(existing.AuthProvider, QmsAuthProviders.EntraId, StringComparison.Ordinal)
                || !string.Equals(existing.EntraObjectId, directoryIdentity.ExternalSubject, StringComparison.Ordinal)))
        {
            throw Error("cross_database_identity_mismatch", StatusCodes.Status409Conflict);
        }

        if (!prepared.IsActive)
        {
            if (existing is not null)
            {
                if (await ActiveSystemAdministratorInvariantGuard.CheckRemovalAsync(
                        connection, transaction, directoryIdentity.UserId, cancellationToken)
                    == ActiveSystemAdministratorGuardResult.Rejected)
                {
                    throw Error("last_system_administrator", StatusCodes.Status409Conflict);
                }

                await using var deactivate = connection.CreateCommand();
                deactivate.Transaction = transaction;
                deactivate.CommandText = "update qms_users set is_active = false where id = @user_id;";
                deactivate.Parameters.AddWithValue("user_id", directoryIdentity.UserId);
                await deactivate.ExecuteNonQueryAsync(cancellationToken);
                await DeactivateWebPushSubscriptionsAsync(
                    connection, transaction, directoryIdentity.UserId, timeProvider.GetUtcNow(), cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var roleAssignments = BuildRoleAssignments(existing, prepared);
        var willBeAdministrator = roleAssignments.Any(assignment =>
            string.Equals(assignment.RoleCode, QmsRoles.SystemAdministrator, StringComparison.Ordinal));
        if (existing is not null && !willBeAdministrator
            && await ActiveSystemAdministratorInvariantGuard.CheckRemovalAsync(
                connection, transaction, directoryIdentity.UserId, cancellationToken)
            == ActiveSystemAdministratorGuardResult.Rejected)
        {
            throw Error("last_system_administrator", StatusCodes.Status409Conflict);
        }

        var departmentChanged = existing?.DepartmentId != prepared.DepartmentId;
        var effectiveDepartmentHead = prepared.IsDepartmentHead
            && (!departmentChanged || prepared.IsDepartmentHeadConfirmed);
        if (existing is null)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into qms_users (
                    id, development_user_key, display_name, department_id, is_active,
                    entra_object_id, email, auth_provider, is_department_head)
                values (
                    @user_id, @development_user_key, @display_name, @department_id, true,
                    @entra_object_id, @email, 'EntraId', @is_department_head);
                """;
            AddIdentityParameters(insert, directoryIdentity, prepared, effectiveDepartmentHead);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                update qms_users
                set display_name = @display_name,
                    email = @email,
                    department_id = @department_id,
                    is_active = true,
                    is_department_head = @is_department_head,
                    deletion_requested_at_utc = null,
                    scheduled_hard_delete_at_utc = null,
                    purge_blocked_at_utc = null,
                    purge_blocked_reason = null,
                    pre_delete_is_active = null
                where id = @user_id;
                """;
            AddIdentityParameters(update, directoryIdentity, prepared, effectiveDepartmentHead);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteRoles = connection.CreateCommand())
        {
            deleteRoles.Transaction = transaction;
            deleteRoles.CommandText = "delete from user_roles where user_id = @user_id;";
            deleteRoles.Parameters.AddWithValue("user_id", directoryIdentity.UserId);
            await deleteRoles.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertRoles = connection.CreateCommand())
        {
            insertRoles.Transaction = transaction;
            insertRoles.CommandText = """
                insert into user_roles (user_id, role_id, assignment_source)
                select @user_id, role.id, assignment.assignment_source
                from unnest(@role_codes, @assignment_sources) assignment(role_code, assignment_source)
                join roles role on role.code = assignment.role_code;
                """;
            insertRoles.Parameters.AddWithValue("user_id", directoryIdentity.UserId);
            insertRoles.Parameters.AddWithValue(
                "role_codes",
                NpgsqlDbType.Array | NpgsqlDbType.Text,
                roleAssignments.Select(assignment => assignment.RoleCode).ToArray());
            insertRoles.Parameters.AddWithValue(
                "assignment_sources",
                NpgsqlDbType.Array | NpgsqlDbType.Text,
                roleAssignments.Select(assignment => assignment.AssignmentSource).ToArray());
            await insertRoles.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static IReadOnlyList<ExistingRoleAssignment> BuildRoleAssignments(
        ExistingLocalProfile? existing,
        PreparedProfile prepared)
    {
        var assignments = (existing?.RoleAssignments ?? [])
            .Where(assignment => string.Equals(
                assignment.AssignmentSource,
                RoleAssignmentSources.Explicit,
                StringComparison.Ordinal))
            .ToDictionary(
                assignment => assignment.RoleCode,
                assignment => assignment.AssignmentSource,
                StringComparer.Ordinal);

        if (prepared.DefaultRoleCode is not null && !assignments.ContainsKey(prepared.DefaultRoleCode))
        {
            assignments[prepared.DefaultRoleCode] = RoleAssignmentSources.DepartmentDefault;
        }
        if (prepared.IsOverallAdministrator && !assignments.ContainsKey(QmsRoles.SystemAdministrator))
        {
            assignments[QmsRoles.SystemAdministrator] = RoleAssignmentSources.OverallAdministrator;
        }

        return assignments
            .OrderBy(assignment => assignment.Key, StringComparer.Ordinal)
            .Select(assignment => new ExistingRoleAssignment(assignment.Key, assignment.Value))
            .ToArray();
    }

    private static void AddIdentityParameters(
        NpgsqlCommand command,
        DirectoryIdentity identity,
        PreparedProfile prepared,
        bool isDepartmentHead)
    {
        command.Parameters.AddWithValue("user_id", identity.UserId);
        command.Parameters.AddWithValue("development_user_key", $"entra:{identity.ExternalSubject}");
        command.Parameters.AddWithValue("display_name", identity.DisplayName);
        command.Parameters.AddWithValue("entra_object_id", identity.ExternalSubject);
        command.Parameters.Add(new NpgsqlParameter("email", NpgsqlDbType.Text)
        {
            Value = identity.Email is null ? DBNull.Value : identity.Email
        });
        command.Parameters.AddWithValue("department_id", prepared.DepartmentId!.Value);
        command.Parameters.AddWithValue("is_department_head", isDepartmentHead);
    }

    private static async Task DeactivateWebPushSubscriptionsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with deactivated as (
                update web_push_subscriptions
                set is_active = false, deactivated_at_utc = @now,
                    deactivation_reason = 'AccountDeactivated', updated_at_utc = @now
                where user_id = @user_id and is_active = true
                returning id)
            insert into web_push_subscription_events (
                subscription_id, user_id, event_type, reason, created_at_utc)
            select id, @user_id, 'AccountDeactivated', 'AccountDeactivated', @now from deactivated;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BusinessState> ReadBusinessStateAsync(
        IReadOnlyList<BusinessUnitAccessAdministrationUser> directoryUsers,
        CancellationToken cancellationToken)
    {
        await using var dataSource = database.RentDataSource(database.GetConnectionString() ?? throw new InvalidOperationException("Database connection is not configured."));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await ValidateBusinessContractAsync(connection, target, cancellationToken);
        var departments = new List<BusinessUnitAccessAdministrationDepartment>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select id, code, name from departments where is_active = true order by sort_order, code;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.GetString(1);
                departments.Add(new BusinessUnitAccessAdministrationDepartment(
                    reader.GetGuid(0),
                    code,
                    reader.GetString(2),
                    DepartmentIdentityPolicy.GetDefaultRoleCode(code)));
            }
        }

        var roles = new List<Role>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select id, code, name from roles order by code;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                roles.Add(new Role(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        var profiles = new Dictionary<Guid, LocalProfile>();
        var userIds = directoryUsers.Select(user => user.UserId).ToArray();
        if (userIds.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select user_account.id, user_account.auth_provider, user_account.entra_object_id,
                       user_account.is_active, user_account.department_id, department.code, department.name,
                       user_account.is_department_head,
                       coalesce(array_agg(role.code order by role.code)
                           filter (where role.code is not null), array[]::text[]),
                       user_account.display_name, user_account.email, user_account.development_user_key,
                       coalesce(array_agg(role.code order by role.code)
                           filter (where role.code is not null
                               and user_role.assignment_source = 'explicit'), array[]::text[])
                from qms_users user_account
                left join departments department on department.id = user_account.department_id
                left join user_roles user_role on user_role.user_id = user_account.id
                left join roles role on role.id = user_role.role_id
                where user_account.id = any(@user_ids)
                group by user_account.id, user_account.auth_provider, user_account.entra_object_id,
                         user_account.is_active, user_account.department_id, department.code,
                         department.name, user_account.is_department_head, user_account.display_name,
                         user_account.email, user_account.development_user_key;
                """;
            command.Parameters.AddWithValue("user_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, userIds);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                profiles.Add(reader.GetGuid(0), new LocalProfile(
                    reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetBoolean(3),
                    reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetFieldValue<string[]>(8), reader.GetBoolean(7),
                    reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.GetString(1) == QmsAuthProviders.EntraId
                        ? (reader.IsDBNull(10) ? null : reader.GetString(10))
                        : reader.GetString(11),
                    reader.GetFieldValue<string[]>(12)));
            }
        }
        return new BusinessState(true, departments, roles, profiles);
    }

    private static async Task<ExistingLocalProfile?> ReadExistingLocalProfileAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select user_account.auth_provider, user_account.entra_object_id, user_account.department_id
            from qms_users user_account
            where user_account.id = @user_id
            for update;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        var authProvider = reader.GetString(0);
        var entraObjectId = reader.IsDBNull(1) ? null : reader.GetString(1);
        var departmentId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
        await reader.CloseAsync();

        await using var roles = connection.CreateCommand();
        roles.Transaction = transaction;
        roles.CommandText = """
            select role.code, user_role.assignment_source
            from user_roles user_role
            join roles role on role.id = user_role.role_id
            where user_role.user_id = @user_id
            order by role.code;
            """;
        roles.Parameters.AddWithValue("user_id", userId);
        var assignments = new List<ExistingRoleAssignment>();
        await using var roleReader = await roles.ExecuteReaderAsync(cancellationToken);
        while (await roleReader.ReadAsync(cancellationToken))
        {
            assignments.Add(new ExistingRoleAssignment(roleReader.GetString(0), roleReader.GetString(1)));
        }
        return new ExistingLocalProfile(authProvider, entraObjectId, departmentId, assignments);
    }

    private async Task ValidateBusinessContractAsync(
        NpgsqlConnection connection, BusinessUnitDatabaseTarget target, CancellationToken cancellationToken)
    {
        var ledger = await businessMigrationLedgerInspector.InspectAsync(connection, target.Code, cancellationToken);
        if (!ledger.MigrationLedgerReady
            || !await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, target, cancellationToken))
            throw new BusinessUnitContextUnavailableException("business_unit_database_contract_mismatch");
    }
    }
}
