using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;

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

        foreach (var identity in identities)
        {
            await using (var identityCommand = directoryConnection.CreateCommand())
            {
                identityCommand.Transaction = transaction;
                identityCommand.CommandText = """
                    insert into directory_identities (
                        user_id, auth_provider, external_subject, is_active)
                    values (@user_id, @auth_provider, @external_subject, true)
                    on conflict (user_id) do update
                    set auth_provider = excluded.auth_provider,
                        external_subject = excluded.external_subject,
                        is_active = true,
                        updated_at_utc = now()
                    where directory_identities.auth_provider = excluded.auth_provider
                      and directory_identities.external_subject = excluded.external_subject;
                    """;
                identityCommand.Parameters.AddWithValue("user_id", identity.UserId);
                identityCommand.Parameters.AddWithValue("auth_provider", identity.AuthProvider);
                identityCommand.Parameters.AddWithValue("external_subject", identity.ExternalSubject);
                if (await identityCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException("Approved identity conflicts with the existing directory identity.");
                }
            }

            await using (var membershipCommand = directoryConnection.CreateCommand())
            {
                membershipCommand.Transaction = transaction;
                membershipCommand.CommandText = """
                    insert into directory_business_unit_memberships (
                        user_id, business_unit_code, is_active)
                    values (@user_id, 'CHEONGJU', true)
                    on conflict (user_id, business_unit_code) do update
                    set is_active = true,
                        updated_at_utc = now();
                    """;
                membershipCommand.Parameters.AddWithValue("user_id", identity.UserId);
                await membershipCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await AppendAuditAsync(
                directoryConnection,
                transaction,
                identity.UserId,
                BusinessUnitCodes.Cheongju,
                "MembershipBackfilled",
                cancellationToken);

            if (overallAdministratorIds.Contains(identity.UserId))
            {
                await using var administratorCommand = directoryConnection.CreateCommand();
                administratorCommand.Transaction = transaction;
                administratorCommand.CommandText = """
                    insert into directory_overall_administrators (user_id, is_active)
                    values (@user_id, true)
                    on conflict (user_id) do update
                    set is_active = true,
                        updated_at_utc = now();
                    """;
                administratorCommand.Parameters.AddWithValue("user_id", identity.UserId);
                await administratorCommand.ExecuteNonQueryAsync(cancellationToken);
                await AppendAuditAsync(
                    directoryConnection,
                    transaction,
                    identity.UserId,
                    null,
                    "OverallAdministratorDesignated",
                    cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Approved Cheongju business-unit memberships were backfilled. IdentityCount={IdentityCount} OverallAdministratorCount={OverallAdministratorCount}.",
            identities.Count,
            overallAdministratorIds.Count);
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
                   case when auth_provider = 'EntraId' then entra_object_id else development_user_key end
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
            identities.Add(new BackfillIdentity(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
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

    private sealed record BackfillIdentity(Guid UserId, string AuthProvider, string ExternalSubject);
}
