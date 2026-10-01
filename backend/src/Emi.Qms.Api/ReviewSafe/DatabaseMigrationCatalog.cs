using System.Globalization;
using System.Text.RegularExpressions;
using Emi.Qms.Api.BusinessUnits;

namespace Emi.Qms.Api.ReviewSafe;

public sealed partial class DatabaseMigrationCatalog
{
    private readonly IWebHostEnvironment? environment;
    private readonly string? migrationsPathOverride;
    private readonly string? businessMigrationsRootPathOverride;

    public DatabaseMigrationCatalog(IWebHostEnvironment environment)
    {
        this.environment = environment;
    }

    private DatabaseMigrationCatalog(
        string migrationsPathOverride,
        string? businessMigrationsRootPathOverride = null)
    {
        this.migrationsPathOverride = Path.GetFullPath(migrationsPathOverride);
        this.businessMigrationsRootPathOverride = businessMigrationsRootPathOverride is null
            ? null
            : Path.GetFullPath(businessMigrationsRootPathOverride);
    }

    public static DatabaseMigrationCatalog FromPath(string migrationsPath)
    {
        return new DatabaseMigrationCatalog(migrationsPath);
    }

    public static DatabaseMigrationCatalog FromPaths(
        string migrationsPath,
        string businessMigrationsRootPath)
    {
        return new DatabaseMigrationCatalog(migrationsPath, businessMigrationsRootPath);
    }

    public string ResolveMigrationsPath()
    {
        if (migrationsPathOverride is not null)
        {
            return Directory.Exists(migrationsPathOverride)
                ? migrationsPathOverride
                : throw new DirectoryNotFoundException("Could not find database/migrations.");
        }

        var contentRootPath = environment?.ContentRootPath
            ?? throw new InvalidOperationException("A web host environment is required to resolve database/migrations.");
        var candidates = new[]
        {
            Path.Combine(contentRootPath, "database", "migrations"),
            Path.Combine(contentRootPath, "..", "database", "migrations"),
            Path.Combine(contentRootPath, "..", "..", "database", "migrations"),
            Path.Combine(contentRootPath, "..", "..", "..", "database", "migrations"),
            Path.Combine(AppContext.BaseDirectory, "database", "migrations"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "migrations"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "migrations")
        };

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(Directory.Exists)
            ?? throw new DirectoryNotFoundException("Could not find database/migrations.");
    }

    public MigrationCatalogSnapshot GetSnapshot()
    {
        return CreateSnapshot(GetSqlFiles(ResolveMigrationsPath()));
    }

    public MigrationCatalogSnapshot GetSnapshot(string businessUnitCode)
    {
        var commonFiles = GetSqlFiles(ResolveMigrationsPath());
        var businessFiles = GetSqlFiles(ResolveBusinessMigrationsPath(businessUnitCode));
        if (businessFiles.Count == 0)
        {
            throw new MigrationCatalogException(
                "migration_catalog_business_target_empty",
                "No migrations were found for the selected business-unit target.");
        }
        return CreateSnapshot(commonFiles.Concat(businessFiles));
    }

    public IReadOnlyList<string> GetCommonMigrationFiles()
    {
        return GetSnapshot().Migrations.Select(item => item.FilePath).ToList();
    }

    public IReadOnlyList<string> GetBusinessMigrationFiles(string businessUnitCode)
    {
        var commonVersions = GetSnapshot().Versions.ToHashSet(StringComparer.Ordinal);
        return GetSnapshot(businessUnitCode).Migrations
            .Where(item => !commonVersions.Contains(item.Version))
            .Select(item => item.FilePath)
            .ToList();
    }

    private MigrationCatalogSnapshot CreateSnapshot(IEnumerable<string> migrationFilePaths)
    {
        var migrationFiles = migrationFilePaths
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
        if (migrationFiles.Count == 0)
        {
            throw new MigrationCatalogException("migration_catalog_empty", "No database migrations were found.");
        }

        var entries = migrationFiles.Select(ParseEntry).ToList();
        var duplicateVersion = entries
            .GroupBy(item => item.Version, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateVersion is not null)
        {
            throw new MigrationCatalogException(
                "migration_catalog_duplicate_version",
                $"Duplicate migration version '{duplicateVersion.Key}' was found.");
        }

        var duplicatePrefix = entries
            .GroupBy(item => item.NumericPrefix)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePrefix is not null)
        {
            throw new MigrationCatalogException(
                "migration_catalog_duplicate_prefix",
                $"Duplicate migration prefix '{duplicatePrefix.Key:D4}' was found.");
        }

        for (var index = 0; index < entries.Count; index += 1)
        {
            var expectedPrefix = index + 1;
            if (entries[index].NumericPrefix != expectedPrefix)
            {
                throw new MigrationCatalogException(
                    "migration_catalog_missing_prefix",
                    $"Migration prefix '{expectedPrefix:D4}' is missing.");
            }
        }

        return new MigrationCatalogSnapshot(entries, entries[^1].Version);
    }

    public IReadOnlyList<string> GetMigrationFiles()
    {
        return GetSnapshot().Migrations.Select(item => item.FilePath).ToList();
    }

    public IReadOnlyList<string> GetMigrationFiles(string businessUnitCode)
    {
        return GetSnapshot(businessUnitCode).Migrations.Select(item => item.FilePath).ToList();
    }

    public string GetExpectedLatestVersion()
    {
        return GetSnapshot().LatestVersion;
    }

    public string GetExpectedLatestVersion(string businessUnitCode)
    {
        return GetSnapshot(businessUnitCode).LatestVersion;
    }

    private string ResolveBusinessMigrationsPath(string businessUnitCode)
    {
        var directoryName = businessUnitCode switch
        {
            BusinessUnitCodes.Cheongju => "cheongju",
            BusinessUnitCodes.Osan => "osan",
            _ => throw new MigrationCatalogException(
                "migration_catalog_business_target_invalid",
                "A known business-unit migration target is required.")
        };

        var root = businessMigrationsRootPathOverride
            ?? Path.Combine(
                Directory.GetParent(ResolveMigrationsPath())?.FullName
                    ?? throw new DirectoryNotFoundException("Could not find database/business-migrations."),
                "business-migrations");
        var path = Path.Combine(root, directoryName);
        return Directory.Exists(path)
            ? path
            : throw new DirectoryNotFoundException("Could not find database business migrations for the selected target.");
    }

    private static IReadOnlyList<string> GetSqlFiles(string path)
    {
        return Directory
            .GetFiles(path, "*.sql", SearchOption.TopDirectoryOnly)
            .ToList();
    }

    private static MigrationCatalogEntry ParseEntry(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = MigrationFileNamePattern().Match(fileName);
        if (!match.Success
            || !int.TryParse(
                match.Groups["prefix"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var prefix))
        {
            throw new MigrationCatalogException(
                "migration_catalog_invalid_filename",
                $"Migration filename '{fileName}' does not match the required ordinal format.");
        }

        return new MigrationCatalogEntry(prefix, Path.GetFileNameWithoutExtension(fileName), filePath);
    }

    [GeneratedRegex("^(?<prefix>[0-9]{4})_[a-z0-9_]+\\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex MigrationFileNamePattern();
}

public sealed record MigrationCatalogEntry(int NumericPrefix, string Version, string FilePath);

public sealed record MigrationCatalogSnapshot(
    IReadOnlyList<MigrationCatalogEntry> Migrations,
    string LatestVersion)
{
    public int ExpectedCount => Migrations.Count;
    public IReadOnlyList<string> Versions => Migrations.Select(item => item.Version).ToList();
}

public sealed class MigrationCatalogException(string reason, string message) : InvalidOperationException(message)
{
    public string Reason { get; } = reason;
}
