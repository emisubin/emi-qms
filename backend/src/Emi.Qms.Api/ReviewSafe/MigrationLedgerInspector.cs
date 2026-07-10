using System.Text.RegularExpressions;
using Npgsql;

namespace Emi.Qms.Api.ReviewSafe;

public sealed partial class MigrationLedgerInspector(DatabaseMigrationCatalog migrationCatalog)
{
    public const string ExactStatus = "Exact";
    public const string CompatibleStatus = "CompatibleWithApprovedLegacy";
    public const string MismatchStatus = "Mismatch";
    public const string UnavailableStatus = "Unavailable";

    private static readonly HashSet<string> ExpectedTeamsActivityChannels = new(StringComparer.Ordinal)
    {
        "TeamsChannel",
        "TeamsDirectMessage",
        "TeamsActivity",
        "Mail"
    };

    public MigrationCatalogSnapshot GetCatalogSnapshot()
    {
        return migrationCatalog.GetSnapshot();
    }

    public async Task<MigrationLedgerInspection> InspectAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        MigrationCatalogSnapshot catalog;
        try
        {
            catalog = migrationCatalog.GetSnapshot();
        }
        catch (MigrationCatalogException exception)
        {
            return MigrationLedgerInspection.CatalogFailure(MismatchStatus, "migration_catalog_invalid", exception.Reason);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MigrationLedgerInspection.CatalogFailure(UnavailableStatus, "migration_ledger_unavailable", null);
        }

        IReadOnlyList<string> actualVersions;
        try
        {
            actualVersions = await ReadAppliedVersionsAsync(connection, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return MigrationLedgerInspection.DatabaseFailure(catalog, "migration_ledger_unavailable");
        }

        var canonicalVersions = catalog.Versions.ToHashSet(StringComparer.Ordinal);
        var actualVersionSet = actualVersions.ToHashSet(StringComparer.Ordinal);
        var missing = catalog.Versions
            .Where(version => !actualVersionSet.Contains(version))
            .ToList();
        var unexpected = actualVersions
            .Where(version => !canonicalVersions.Contains(version))
            .OrderBy(version => version, StringComparer.Ordinal)
            .ToList();
        var actualLatest = actualVersions.LastOrDefault();
        var approvedPolicy = MigrationLedgerCompatibilityPolicy.ApprovedLegacyMigrations.Single();
        var hasApprovedLegacy = unexpected.Contains(approvedPolicy.LegacyVersion, StringComparer.Ordinal);

        if (hasApprovedLegacy && !actualVersionSet.Contains(approvedPolicy.CanonicalSuccessor))
        {
            return MigrationLedgerInspection.Mismatch(
                catalog,
                actualVersions.Count,
                actualLatest,
                missing,
                unexpected,
                "migration_ledger_legacy_successor_missing");
        }

        if (missing.Count > 0)
        {
            return MigrationLedgerInspection.Mismatch(
                catalog,
                actualVersions.Count,
                actualLatest,
                missing,
                unexpected,
                "migration_ledger_missing");
        }

        var unknownUnexpected = unexpected
            .Where(version => !string.Equals(version, approvedPolicy.LegacyVersion, StringComparison.Ordinal))
            .ToList();
        if (unknownUnexpected.Count > 0 || unexpected.Count > 1)
        {
            return MigrationLedgerInspection.Mismatch(
                catalog,
                actualVersions.Count,
                actualLatest,
                missing,
                unexpected,
                "migration_ledger_unexpected");
        }

        var schemaCompatible = await ProbeTeamsActivitySchemaAsync(connection, cancellationToken);
        if (!schemaCompatible)
        {
            return MigrationLedgerInspection.Mismatch(
                catalog,
                actualVersions.Count,
                actualLatest,
                missing,
                unexpected,
                hasApprovedLegacy
                    ? "migration_ledger_legacy_schema_mismatch"
                    : "migration_ledger_schema_mismatch");
        }

        if (hasApprovedLegacy)
        {
            return MigrationLedgerInspection.Ready(
                CompatibleStatus,
                catalog,
                actualVersions.Count,
                actualLatest,
                [approvedPolicy.LegacyVersion]);
        }

        return MigrationLedgerInspection.Ready(
            ExactStatus,
            catalog,
            actualVersions.Count,
            actualLatest,
            []);
    }

    private static async Task<IReadOnlyList<string>> ReadAppliedVersionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select version from schema_migrations order by version;";
        var versions = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetString(0));
        }

        return versions;
    }

    private static async Task<bool> ProbeTeamsActivitySchemaAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select pg_get_constraintdef(constraint_row.oid)
            from pg_constraint constraint_row
            join pg_class table_row on table_row.oid = constraint_row.conrelid
            join pg_namespace schema_row on schema_row.oid = table_row.relnamespace
            where schema_row.nspname = 'public'
              and table_row.relname = 'notification_deliveries'
              and constraint_row.conname = 'ck_notification_deliveries_channel';
            """;
        var definition = (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
        if (string.IsNullOrWhiteSpace(definition))
        {
            return false;
        }

        var channels = SingleQuotedValuePattern()
            .Matches(definition)
            .Select(match => match.Groups["value"].Value)
            .ToHashSet(StringComparer.Ordinal);
        return channels.SetEquals(ExpectedTeamsActivityChannels);
    }

    [GeneratedRegex("'(?<value>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex SingleQuotedValuePattern();
}

public sealed record MigrationLedgerInspection(
    string Status,
    string Reason,
    int ExpectedMigrationCount,
    int? ActualMigrationCount,
    string? ExpectedLatestMigration,
    string? ActualLatestMigration,
    IReadOnlyList<string> MissingMigrations,
    IReadOnlyList<string> UnexpectedMigrations,
    IReadOnlyList<string> ApprovedLegacyMigrations,
    bool MigrationSchemaCompatible,
    bool MigrationLedgerReady,
    string? CatalogError)
{
    public static MigrationLedgerInspection Ready(
        string status,
        MigrationCatalogSnapshot catalog,
        int actualCount,
        string? actualLatest,
        IReadOnlyList<string> approvedLegacy)
    {
        return new MigrationLedgerInspection(
            status,
            "ready",
            catalog.ExpectedCount,
            actualCount,
            catalog.LatestVersion,
            actualLatest,
            [],
            [],
            approvedLegacy,
            true,
            true,
            null);
    }

    public static MigrationLedgerInspection Mismatch(
        MigrationCatalogSnapshot catalog,
        int actualCount,
        string? actualLatest,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> unexpected,
        string reason)
    {
        return new MigrationLedgerInspection(
            MigrationLedgerInspector.MismatchStatus,
            reason,
            catalog.ExpectedCount,
            actualCount,
            catalog.LatestVersion,
            actualLatest,
            Limit(missing),
            Limit(unexpected),
            [],
            false,
            false,
            null);
    }

    public static MigrationLedgerInspection CatalogFailure(string status, string reason, string? catalogError)
    {
        return new MigrationLedgerInspection(
            status,
            reason,
            0,
            null,
            null,
            null,
            [],
            [],
            [],
            false,
            false,
            catalogError);
    }

    public static MigrationLedgerInspection DatabaseFailure(MigrationCatalogSnapshot catalog, string reason)
    {
        return new MigrationLedgerInspection(
            MigrationLedgerInspector.UnavailableStatus,
            reason,
            catalog.ExpectedCount,
            null,
            catalog.LatestVersion,
            null,
            [],
            [],
            [],
            false,
            false,
            null);
    }

    private static IReadOnlyList<string> Limit(IReadOnlyList<string> values)
    {
        return values.Take(20).ToList();
    }
}
