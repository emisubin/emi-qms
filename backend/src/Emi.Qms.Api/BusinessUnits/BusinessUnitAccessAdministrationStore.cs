using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.BusinessUnits;

public sealed record BusinessUnitAccessAdministrationUser(
    Guid UserId,
    string AuthProvider,
    string DisplayName,
    string? Email,
    IReadOnlyList<string> Memberships,
    bool IsOverallAdministrator);

public sealed record BusinessUnitAccessAdministrationSnapshot(
    IReadOnlyList<BusinessUnitAccessAdministrationUser> Users,
    IReadOnlyList<string> AvailableBusinessUnits);

public sealed record BusinessUnitMembershipUpdateResult(
    bool Changed,
    BusinessUnitAccessAdministrationSnapshot Snapshot);

public sealed class BusinessUnitAccessAdministrationStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    BusinessUnitDirectoryMigrationCatalog directoryMigrationCatalog)
{
    public async Task<BusinessUnitAccessAdministrationSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await ValidateContractAsync(connection, cancellationToken);
        return await ReadSnapshotAsync(connection, null, cancellationToken);
    }

    public async Task<BusinessUnitMembershipUpdateResult> SetMembershipsAsync(
        Guid targetUserId,
        IReadOnlyCollection<string> businessUnitCodes,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (businessUnitCodes.Any(string.IsNullOrWhiteSpace))
        {
            throw new BusinessUnitAccessAdministrationException(
                "business_unit_unknown",
                StatusCodes.Status400BadRequest);
        }

        var normalized = businessUnitCodes
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Any(code => !BusinessUnitCodes.IsKnown(code)))
        {
            throw new BusinessUnitAccessAdministrationException(
                "business_unit_unknown",
                StatusCodes.Status400BadRequest);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await ValidateContractAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                select set_directory_business_unit_memberships(
                    @event_id,
                    @target_user_id,
                    @business_unit_codes,
                    @actor_user_id);
                """;
            command.Parameters.AddWithValue("event_id", Guid.NewGuid());
            command.Parameters.AddWithValue("target_user_id", targetUserId);
            command.Parameters.AddWithValue(
                "business_unit_codes",
                NpgsqlDbType.Array | NpgsqlDbType.Text,
                normalized);
            command.Parameters.AddWithValue("actor_user_id", actorUserId);
            var changed = await command.ExecuteScalarAsync(cancellationToken) is true;
            var snapshot = await ReadSnapshotAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new BusinessUnitMembershipUpdateResult(changed, snapshot);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new BusinessUnitAccessAdministrationException(
                "overall_administrator_required",
                StatusCodes.Status403Forbidden,
                exception);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.NoDataFound)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new BusinessUnitAccessAdministrationException(
                "directory_identity_not_found",
                StatusCodes.Status404NotFound,
                exception);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InvalidParameterValue)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new BusinessUnitAccessAdministrationException(
                "business_unit_unknown",
                StatusCodes.Status400BadRequest,
                exception);
        }
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var directory = connectionStringProvider.BusinessUnits.Directory
            ?? throw new BusinessUnitContextUnavailableException("directory_not_configured");
        var connectionString = connectionStringProvider.GetConnectionString(
            directory,
            BusinessUnitConnectionPurpose.Runtime);
        return NpgsqlDataSource.Create(connectionString);
    }

    private async Task ValidateContractAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var directory = connectionStringProvider.BusinessUnits.Directory
            ?? throw new BusinessUnitContextUnavailableException("directory_not_configured");
        var ledger = await directoryMigrationCatalog.InspectAsync(connection, cancellationToken);
        if (!ledger.MigrationLedgerReady
            || !await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, directory, cancellationToken))
        {
            throw new BusinessUnitContextUnavailableException("directory_database_contract_mismatch");
        }
    }

    private static async Task<BusinessUnitAccessAdministrationSnapshot> ReadSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var availableBusinessUnits = new List<string>();
        await using (var unitCommand = connection.CreateCommand())
        {
            unitCommand.Transaction = transaction;
            unitCommand.CommandText = """
                select code
                from directory_business_units
                where is_active = true
                order by code;
                """;
            await using var reader = await unitCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                availableBusinessUnits.Add(reader.GetString(0));
            }
        }

        var users = new List<BusinessUnitAccessAdministrationUser>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select identity_row.user_id,
                       identity_row.auth_provider,
                       coalesce(
                           nullif(btrim(identity_row.display_name), ''),
                           case
                               when identity_row.auth_provider = 'EntraId'
                                   then 'Microsoft 365 사용자 (정보 확인 필요)'
                               else identity_row.external_subject
                           end) as display_name,
                       identity_row.email,
                       coalesce(
                           array_agg(membership.business_unit_code order by membership.business_unit_code)
                               filter (where business_unit.code is not null),
                           array[]::text[]),
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
                where identity_row.is_active = true
                group by identity_row.user_id,
                         identity_row.auth_provider,
                         identity_row.external_subject,
                         identity_row.display_name,
                         identity_row.email
                order by display_name, identity_row.email, identity_row.user_id;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                users.Add(new BusinessUnitAccessAdministrationUser(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetFieldValue<string[]>(4),
                    reader.GetBoolean(5)));
            }
        }

        return new BusinessUnitAccessAdministrationSnapshot(users, availableBusinessUnits);
    }
}

public sealed class BusinessUnitAccessAdministrationException(
    string errorCode,
    int statusCode,
    Exception? innerException = null)
    : InvalidOperationException(errorCode, innerException)
{
    public string ErrorCode { get; } = errorCode;
    public int StatusCode { get; } = statusCode;
}
