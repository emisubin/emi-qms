namespace Emi.Qms.Api.ReviewSafe;

public sealed class DatabaseMigrationCatalog(IWebHostEnvironment environment)
{
    public string ResolveMigrationsPath()
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "database", "migrations"),
            Path.Combine(environment.ContentRootPath, "..", "database", "migrations"),
            Path.Combine(environment.ContentRootPath, "..", "..", "database", "migrations"),
            Path.Combine(environment.ContentRootPath, "..", "..", "..", "database", "migrations"),
            Path.Combine(AppContext.BaseDirectory, "database", "migrations"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "migrations"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "migrations")
        };

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(Directory.Exists)
            ?? throw new DirectoryNotFoundException("Could not find database/migrations.");
    }

    public IReadOnlyList<string> GetMigrationFiles()
    {
        return Directory
            .GetFiles(ResolveMigrationsPath(), "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
    }

    public string GetExpectedLatestVersion()
    {
        var migration = GetMigrationFiles().LastOrDefault();
        return migration is null
            ? throw new InvalidOperationException("No database migrations were found.")
            : Path.GetFileNameWithoutExtension(migration);
    }
}
