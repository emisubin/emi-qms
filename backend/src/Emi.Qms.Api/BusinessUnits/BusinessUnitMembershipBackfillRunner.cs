using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.BusinessUnits;

public sealed class BusinessUnitMembershipBackfillRunner(
    DatabaseConnectionStringProvider connectionStringProvider,
    IConfiguration configuration,
    ILogger<BusinessUnitMembershipBackfillRunner> logger,
    MigrationLedgerInspector migrationLedgerInspector,
    BusinessUnitDirectoryMigrationCatalog directoryMigrationCatalog)
{
    public async Task<int> ApplyAsync(CancellationToken cancellationToken)
    {
        if (ReviewSafeMode.IsEnabled(configuration))
        {
            throw new InvalidOperationException(
                "Business-unit membership backfill is disabled in review-safe UAT mode.");
        }

        var businessUnits = connectionStringProvider.BusinessUnits;
        if (!businessUnits.Enabled || businessUnits.Directory is null)
        {
            throw new InvalidOperationException("Business-unit membership backfill requires enabled multi-database mode.");
        }

        var approvedUserIds = ReadApprovedIds("ApprovedUserIds");
        if (approvedUserIds.Count == 0)
        {
            throw new InvalidOperationException("No explicitly approved membership backfill identities were configured.");
        }

        var overallAdministratorIds = ReadApprovedIds("OverallAdministratorUserIds");
        if (overallAdministratorIds.Any(id => !approvedUserIds.Contains(id)))
        {
            throw new InvalidOperationException("Overall administrator designations must be a subset of approved backfill identities.");
        }

        var cheongju = businessUnits.GetBusiness(BusinessUnitCodes.Cheongju);
        var identities = await ReadApprovedCheongjuIdentitiesAsync(
            cheongju,
            approvedUserIds,
            cancellationToken);
        if (identities.Count != approvedUserIds.Count)
        {
            throw new InvalidOperationException("One or more approved backfill identities were not active Cheongju identities.");
        }

        if (identities.Any(identity => overallAdministratorIds.Contains(identity.UserId)
                && (!identity.IsSystemAdministrator || !identity.IsReadyForCheongjuMembership)))
        {
            throw new InvalidOperationException("Every approved overall administrator must already be a Cheongju system administrator.");
        }

        var osan = businessUnits.GetBusiness(BusinessUnitCodes.Osan);
        foreach (var identity in identities.Where(identity => overallAdministratorIds.Contains(identity.UserId)))
        {
            await EnsureOverallAdministratorProfileAsync(osan, identity, cancellationToken);
        }

        var directoryConnectionString = connectionStringProvider.GetConnectionString(
            businessUnits.Directory,
            BusinessUnitConnectionPurpose.Migration);
        await using var directoryDataSource = NpgsqlDataSource.Create(directoryConnectionString);
        await using var directoryConnection = await directoryDataSource.OpenConnectionAsync(cancellationToken);
        await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(
            directoryConnection,
            businessUnits.Directory,
            cancellationToken);
        if (!(await directoryMigrationCatalog.InspectAsync(
                directoryConnection,
                cancellationToken)).MigrationLedgerReady)
        {
            throw new InvalidOperationException("Directory database migration ledger is not ready for backfill.");
        }
        await using var transaction = await directoryConnection.BeginTransactionAsync(cancellationToken);

        var deactivatedMembershipCount = 0;
        var activatedMembershipCount = 0;
        foreach (var identity in identities)
        {
            await using (var identityCommand = directoryConnection.CreateCommand())
            {
                identityCommand.Transaction = transaction;
                identityCommand.CommandText = """
                    insert into directory_identities (
                        user_id, auth_provider, external_subject, display_name, email, is_active)
                    values (@user_id, @auth_provider, @external_subject, @display_name, @email, true)
                    on conflict (user_id) do update
                    set auth_provider = excluded.auth_provider,
                        external_subject = excluded.external_subject,
                        display_name = excluded.display_name,
                        email = excluded.email,
                        is_active = true,
                        updated_at_utc = now()
                    where directory_identities.auth_provider = excluded.auth_provider
                      and directory_identities.external_subject = excluded.external_subject;
                    """;
                identityCommand.Parameters.AddWithValue("user_id", identity.UserId);
                identityCommand.Parameters.AddWithValue("auth_provider", identity.AuthProvider);
                identityCommand.Parameters.AddWithValue("external_subject", identity.ExternalSubject);
                identityCommand.Parameters.AddWithValue("display_name", identity.DisplayName);
                identityCommand.Parameters.Add(new NpgsqlParameter("email", NpgsqlDbType.Text)
                {
                    Value = identity.Email is null ? DBNull.Value : identity.Email
                });
                if (await identityCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException("Approved identity conflicts with the existing directory identity.");
                }
            }

            var currentMemberships = new HashSet<string>(StringComparer.Ordinal);
            await using (var currentMembershipCommand = directoryConnection.CreateCommand())
            {
                currentMembershipCommand.Transaction = transaction;
                currentMembershipCommand.CommandText = """
                    select business_unit_code
                    from directory_business_unit_memberships
                    where user_id = @user_id and is_active = true
                    order by business_unit_code
                    for update;
                    """;
                currentMembershipCommand.Parameters.AddWithValue("user_id", identity.UserId);
                await using var reader = await currentMembershipCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    currentMemberships.Add(reader.GetString(0));
                }
            }

            if (!overallAdministratorIds.Contains(identity.UserId) && currentMemberships.Count > 1)
            {
                throw new InvalidOperationException(
                    "An ordinary approved identity already has multiple active business-unit memberships.");
            }

            var desiredMemberships = overallAdministratorIds.Contains(identity.UserId)
                ? new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan }
                : currentMemberships.Contains(BusinessUnitCodes.Osan)
                    ? new[] { BusinessUnitCodes.Osan }
                    : identity.IsReadyForCheongjuMembership
                        ? new[] { BusinessUnitCodes.Cheongju }
                        : [];

            await using (var revokeCommand = directoryConnection.CreateCommand())
            {
                revokeCommand.Transaction = transaction;
                revokeCommand.CommandText = """
                    update directory_business_unit_memberships
                    set is_active = false,
                        updated_at_utc = now()
                    where user_id = @user_id
                      and is_active = true
                      and not (business_unit_code = any(@business_unit_codes));
                    """;
                revokeCommand.Parameters.AddWithValue("user_id", identity.UserId);
                revokeCommand.Parameters.AddWithValue(
                    "business_unit_codes",
                    NpgsqlDbType.Array | NpgsqlDbType.Text,
                    desiredMemberships);
                var revoked = await revokeCommand.ExecuteNonQueryAsync(cancellationToken);
                if (revoked > 0)
                {
                    deactivatedMembershipCount += revoked;
                    await AppendAuditAsync(
                        directoryConnection,
                        transaction,
                        identity.UserId,
                        null,
                        "MembershipReadinessRevoked",
                        cancellationToken);
                }
            }

            await using (var membershipCommand = directoryConnection.CreateCommand())
            {
                membershipCommand.Transaction = transaction;
                membershipCommand.CommandText = """
                    insert into directory_business_unit_memberships (
                        user_id, business_unit_code, is_active)
                    select @user_id, business_unit_code, true
                    from unnest(@business_unit_codes) business_unit_code
                    on conflict (user_id, business_unit_code) do update
                    set is_active = true,
                        updated_at_utc = now()
                    where directory_business_unit_memberships.is_active = false;
                    """;
                membershipCommand.Parameters.AddWithValue("user_id", identity.UserId);
                membershipCommand.Parameters.AddWithValue(
                    "business_unit_codes",
                    NpgsqlDbType.Array | NpgsqlDbType.Text,
                    desiredMemberships);
                var activated = await membershipCommand.ExecuteNonQueryAsync(cancellationToken);
                if (activated > 0)
                {
                    activatedMembershipCount += activated;
                    await AppendAuditAsync(
                        directoryConnection,
                        transaction,
                        identity.UserId,
                        null,
                        "MembershipBackfilled",
                        cancellationToken);
                }
            }

            if (overallAdministratorIds.Contains(identity.UserId))
            {
                await using var administratorCommand = directoryConnection.CreateCommand();
                administratorCommand.Transaction = transaction;
                administratorCommand.CommandText = """
                    insert into directory_overall_administrators (user_id, is_active)
                    values (@user_id, true)
                    on conflict (user_id) do update
                    set is_active = true,
                        updated_at_utc = now()
                    where directory_overall_administrators.is_active = false;
                    """;
                administratorCommand.Parameters.AddWithValue("user_id", identity.UserId);
                if (await administratorCommand.ExecuteNonQueryAsync(cancellationToken) > 0)
                {
                    await AppendAuditAsync(
                        directoryConnection,
                        transaction,
                        identity.UserId,
                        null,
                        "OverallAdministratorDesignated",
                        cancellationToken);
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Approved business-unit memberships were reconciled. IdentityCount={IdentityCount} OverallAdministratorCount={OverallAdministratorCount} ActivatedMembershipCount={ActivatedMembershipCount} DeactivatedMembershipCount={DeactivatedMembershipCount}.",
            identities.Count,
            overallAdministratorIds.Count,
            activatedMembershipCount,
            deactivatedMembershipCount);
        return identities.Count;
    }

    private async Task<IReadOnlyList<BackfillIdentity>> ReadApprovedCheongjuIdentitiesAsync(
        BusinessUnitDatabaseTarget cheongju,
        IReadOnlySet<Guid> approvedUserIds,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString(
            cheongju,
            BusinessUnitConnectionPurpose.Migration);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, cheongju, cancellationToken))
        {
            throw new InvalidOperationException("Cheongju database identity is not bound to the approved source database.");
        }
        if (!(await migrationLedgerInspector.InspectAsync(
                connection,
                cancellationToken)).MigrationLedgerReady)
        {
            throw new InvalidOperationException("Cheongju database migration ledger is not ready for backfill.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id,
                   auth_provider,
                   case when auth_provider = 'EntraId' then entra_object_id else development_user_key end,
                   display_name,
                   email,
                   exists (
                       select 1 from user_roles user_role
                       join roles role on role.id = user_role.role_id
                       where user_role.user_id = qms_users.id and role.code = 'system-administrator'),
                   qms_users.department_id is not null
                   and exists (
                       select 1
                       from departments department
                       join roles default_role on default_role.code = case department.code
                           when 'administration' then 'system-administrator'
                           when 'sales' then 'sales'
                           when 'design' then 'design'
                           when 'production-planning' then 'production-planning'
                           when 'procurement' then 'procurement'
                           when 'materials' then 'materials'
                           when 'manufacturing' then 'manufacturing'
                           when 'quality' then 'quality'
                           when 'logistics' then 'logistics'
                           when 'readonly' then 'read-only'
                           else null
                       end
                       join user_roles default_assignment
                         on default_assignment.role_id = default_role.id
                        and default_assignment.user_id = qms_users.id
                       where department.id = qms_users.department_id
                         and department.is_active = true)
            from qms_users
            where id = any(@approved_user_ids)
              and is_active = true
              and auth_provider in ('EntraId', 'Dev')
              and case when auth_provider = 'EntraId' then entra_object_id else development_user_key end is not null;
            """;
        command.Parameters.AddWithValue("approved_user_ids", approvedUserIds.ToArray());
        var identities = new List<BackfillIdentity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            identities.Add(new BackfillIdentity(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetBoolean(5), reader.GetBoolean(6)));
        }
        return identities;
    }

    private HashSet<Guid> ReadApprovedIds(string key)
    {
        var configuredItems = configuration
            .GetSection($"{BusinessUnitConfiguration.SectionName}:MembershipBackfill:{key}")
            .Get<string[]>() ?? [];
        var delimitedItems = (configuration[
                $"{BusinessUnitConfiguration.SectionName}:MembershipBackfill:{key}Delimited"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var raw = configuredItems.Concat(delimitedItems);
        var ids = new HashSet<Guid>();
        foreach (var value in raw)
        {
            if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
            {
                throw new InvalidOperationException("Membership backfill contains an invalid approved identity.");
            }
            ids.Add(id);
        }
        return ids;
    }

    private static async Task AppendAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        string? businessUnitCode,
        string action,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into directory_membership_audit_events (
                id, user_id, business_unit_code, action, actor_kind)
            values (@id, @user_id, @business_unit_code, @action, 'ApprovedBootstrap');
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("business_unit_code", businessUnitCode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("action", action);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureOverallAdministratorProfileAsync(
        BusinessUnitDatabaseTarget target,
        BackfillIdentity identity,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString(
            target,
            BusinessUnitConnectionPurpose.Migration);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, target, cancellationToken)
            || !(await migrationLedgerInspector.InspectAsync(connection, cancellationToken)).MigrationLedgerReady)
        {
            throw new InvalidOperationException("Overall administrator target database is not ready for backfill.");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var identityCommand = connection.CreateCommand())
        {
            identityCommand.Transaction = transaction;
            identityCommand.CommandText = """
                insert into qms_users (
                    id, development_user_key, display_name, department_id, is_active,
                    entra_object_id, email, auth_provider, is_department_head)
                select @user_id, @development_user_key, @display_name, department.id, true,
                       @entra_object_id, @email, @auth_provider, false
                from departments department
                where department.code = 'administration' and department.is_active = true
                on conflict (id) do update
                set display_name = excluded.display_name,
                    email = excluded.email,
                    is_active = true,
                    deletion_requested_at_utc = null,
                    scheduled_hard_delete_at_utc = null,
                    purge_blocked_at_utc = null,
                    purge_blocked_reason = null,
                    pre_delete_is_active = null
                where qms_users.auth_provider = excluded.auth_provider
                  and coalesce(qms_users.entra_object_id, qms_users.development_user_key)
                      = coalesce(excluded.entra_object_id, excluded.development_user_key);
                """;
            identityCommand.Parameters.AddWithValue("user_id", identity.UserId);
            identityCommand.Parameters.AddWithValue(
                "development_user_key",
                identity.AuthProvider == QmsAuthProviders.EntraId
                    ? $"entra:{identity.ExternalSubject}"
                    : identity.ExternalSubject);
            identityCommand.Parameters.AddWithValue("display_name", identity.DisplayName);
            identityCommand.Parameters.Add(new NpgsqlParameter("entra_object_id", NpgsqlDbType.Text)
            {
                Value = identity.AuthProvider == QmsAuthProviders.EntraId ? identity.ExternalSubject : DBNull.Value
            });
            identityCommand.Parameters.Add(new NpgsqlParameter("email", NpgsqlDbType.Text)
            {
                Value = identity.Email is null ? DBNull.Value : identity.Email
            });
            identityCommand.Parameters.AddWithValue("auth_provider", identity.AuthProvider);
            if (await identityCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("Overall administrator identity conflicts with the target business database.");
            }
        }

        await using (var roleCommand = connection.CreateCommand())
        {
            roleCommand.Transaction = transaction;
            roleCommand.CommandText = """
                insert into user_roles (user_id, role_id, assignment_source)
                select @user_id, role.id, 'overall-administrator'
                from roles role where role.code = 'system-administrator'
                on conflict (user_id, role_id) do update
                set assignment_source = case
                    when user_roles.assignment_source = 'explicit' then 'explicit'
                    else excluded.assignment_source
                end;
                """;
            roleCommand.Parameters.AddWithValue("user_id", identity.UserId);
            await roleCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record BackfillIdentity(
        Guid UserId,
        string AuthProvider,
        string ExternalSubject,
        string DisplayName,
        string? Email,
        bool IsSystemAdministrator,
        bool IsReadyForCheongjuMembership);
}
