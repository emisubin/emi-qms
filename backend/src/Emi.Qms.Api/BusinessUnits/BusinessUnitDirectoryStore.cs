using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.BusinessUnits;

public sealed record BusinessUnitDirectorySnapshot(
    Guid UserId,
    IReadOnlyList<string> ActiveMemberships,
    bool IsOverallAdministrator);

public sealed class BusinessUnitDirectoryStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    BusinessUnitDirectoryMigrationCatalog directoryMigrationCatalog)
{
    public async Task<Guid> RegisterOrUpdatePendingEntraIdentityAsync(
        string externalSubject,
        string displayName,
        string? email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalSubject))
        {
            throw new ArgumentException("Entra object id is required.", nameof(externalSubject));
        }

        var directory = connectionStringProvider.BusinessUnits.Directory
            ?? throw new BusinessUnitContextUnavailableException("directory_not_configured");
        var connectionString = connectionStringProvider.GetConnectionString(
            directory,
            BusinessUnitConnectionPurpose.Runtime);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var ledger = await directoryMigrationCatalog.InspectAsync(connection, cancellationToken);
        if (!ledger.MigrationLedgerReady
            || !await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, directory, cancellationToken))
        {
            throw new BusinessUnitContextUnavailableException("directory_database_contract_mismatch");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select register_or_update_pending_entra_directory_identity(
                @proposed_user_id,
                @external_subject,
                @display_name,
                @email);
            """;
        command.Parameters.AddWithValue("proposed_user_id", Guid.NewGuid());
        command.Parameters.AddWithValue("external_subject", externalSubject.Trim());
        command.Parameters.AddWithValue(
            "display_name",
            string.IsNullOrWhiteSpace(displayName) ? "Microsoft 365 사용자" : displayName.Trim());
        command.Parameters.Add(new NpgsqlParameter("email", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(email) ? DBNull.Value : email.Trim()
        });

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid userId
            ? userId
            : throw new BusinessUnitContextUnavailableException("directory_identity_registration_failed");
    }

    public async Task<BusinessUnitDirectorySnapshot?> FindAsync(
        string authProvider,
        string externalSubject,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authProvider) || string.IsNullOrWhiteSpace(externalSubject))
        {
            return null;
        }

        var directory = connectionStringProvider.BusinessUnits.Directory
            ?? throw new BusinessUnitContextUnavailableException("directory_not_configured");
        var connectionString = connectionStringProvider.GetConnectionString(
            directory,
            BusinessUnitConnectionPurpose.Runtime);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var ledger = await directoryMigrationCatalog.InspectAsync(connection, cancellationToken);
        if (!ledger.MigrationLedgerReady
            || !await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, directory, cancellationToken))
        {
            throw new BusinessUnitContextUnavailableException("directory_database_contract_mismatch");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select identity_row.user_id,
                   coalesce(array_agg(membership.business_unit_code order by membership.business_unit_code)
                       filter (where business_unit.code is not null), array[]::text[]),
                   exists (
                       select 1
                       from directory_overall_administrators administrator
                       where administrator.user_id = identity_row.user_id
                         and administrator.is_active = true)
            from directory_identities identity_row
            left join directory_business_unit_memberships membership
                on membership.user_id = identity_row.user_id
               and membership.is_active = true
            left join directory_business_units business_unit
                on business_unit.code = membership.business_unit_code
               and business_unit.is_active = true
            where identity_row.auth_provider = @auth_provider
              and identity_row.external_subject = @external_subject
              and identity_row.is_active = true
              and exists (
                  select 1
                  from qms_database_identity database_identity
                  where database_identity.singleton = true
                    and database_identity.database_kind = 'directory'
                    and database_identity.schema_contract = @schema_contract)
            group by identity_row.user_id;
            """;
        command.Parameters.AddWithValue("auth_provider", authProvider.Trim());
        command.Parameters.AddWithValue("external_subject", externalSubject.Trim());
        command.Parameters.AddWithValue("schema_contract", BusinessUnitConfiguration.DirectorySchemaVersion);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BusinessUnitDirectorySnapshot(
            reader.GetGuid(0),
            reader.GetFieldValue<string[]>(1)
                .Where(BusinessUnitCodes.IsKnown)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToList(),
            reader.GetBoolean(2));
    }
}
