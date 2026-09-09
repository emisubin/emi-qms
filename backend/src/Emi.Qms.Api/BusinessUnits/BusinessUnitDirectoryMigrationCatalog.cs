using Emi.Qms.Api.ReviewSafe;
using Npgsql;

namespace Emi.Qms.Api.BusinessUnits;

public sealed class BusinessUnitDirectoryMigrationCatalog(DatabaseMigrationCatalog businessCatalog)
{
    public DatabaseMigrationCatalog Catalog { get; } = DatabaseMigrationCatalog.FromPath(
        Path.Combine(
            Directory.GetParent(businessCatalog.ResolveMigrationsPath())?.FullName
                ?? throw new DirectoryNotFoundException("Could not resolve the database directory."),
            "directory-migrations"));

    public async Task<MigrationLedgerInspection> InspectAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var snapshot = Catalog.GetSnapshot();
        var versions = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select version from schema_migrations order by version;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) versions.Add(reader.GetString(0));
        }

        var missing = snapshot.Versions.Where(version => !versions.Contains(version, StringComparer.Ordinal)).ToList();
        var unexpected = versions.Where(version => !snapshot.Versions.Contains(version, StringComparer.Ordinal)).ToList();
        if (missing.Count != 0 || unexpected.Count != 0)
        {
            return MigrationLedgerInspection.Mismatch(
                snapshot,
                versions.Count,
                versions.LastOrDefault(),
                missing,
                unexpected,
                "directory_migration_ledger_mismatch");
        }

        await using var identityCommand = connection.CreateCommand();
        identityCommand.CommandText = """
            select exists (
                select 1 from qms_database_identity
                where singleton = true
                  and database_kind = 'directory'
                  and business_unit_code is null
                  and schema_contract = @schema_contract);
            """;
        identityCommand.Parameters.AddWithValue("schema_contract", BusinessUnitConfiguration.DirectorySchemaVersion);
        var identityReady = await identityCommand.ExecuteScalarAsync(cancellationToken) is true;
        return identityReady
            ? MigrationLedgerInspection.Ready(
                MigrationLedgerInspector.ExactStatus,
                snapshot,
                versions.Count,
                versions.LastOrDefault(),
                [])
            : MigrationLedgerInspection.Mismatch(
                snapshot,
                versions.Count,
                versions.LastOrDefault(),
                [],
                [],
                "directory_database_identity_mismatch");
    }
}
