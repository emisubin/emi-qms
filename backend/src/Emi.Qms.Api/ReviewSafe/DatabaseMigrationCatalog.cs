using System.Globalization;
using System.Text.RegularExpressions;

namespace Emi.Qms.Api.ReviewSafe;

public sealed partial class DatabaseMigrationCatalog
{
    private readonly IWebHostEnvironment? environment;
    private readonly string? migrationsPathOverride;

    public DatabaseMigrationCatalog(IWebHostEnvironment environment)
    {
        this.environment = environment;
    }

    private DatabaseMigrationCatalog(string migrationsPathOverride)
    {
        this.migrationsPathOverride = Path.GetFullPath(migrationsPathOverride);
    }

    public static DatabaseMigrationCatalog FromPath(string migrationsPath)
    {
        return new DatabaseMigrationCatalog(migrationsPath);
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
        var migrationFiles = Directory
            .GetFiles(ResolveMigrationsPath(), "*.sql", SearchOption.TopDirectoryOnly)
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

    public string GetExpectedLatestVersion()
    {
        return GetSnapshot().LatestVersion;
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
