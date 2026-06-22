using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class PostgreSqlMigrationTests
{
    [Fact]
    public async Task SchemaMigration_AppliesWithoutFakeDevelopmentData()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);
        var runner = CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider);

        await runner.ApplyAsync(TestContext.Current.CancellationToken);

        var counts = await ReadCountsAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        Assert.Equal(0, counts.Users);
        Assert.Equal(0, counts.Departments);
        Assert.Equal(0, counts.Projects);
        Assert.Equal(0, counts.ProjectAccess);
        Assert.Equal(7, counts.Roles);
        Assert.Equal(9, counts.Permissions);
        Assert.True(counts.RolePermissions > 0);

        await AssertCoreConstraintsExistAsync(connectionStringProvider, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Development", "DevelopmentData:SeedEnabled")]
    [InlineData("Testing", "DevelopmentData:SeedEnabled")]
    [InlineData("Testing", "DEV_DATA_SEED_ENABLED")]
    public async Task DevelopmentDataSeeder_CreatesFakeDataOnlyWhenExplicitlyEnabled_AndIsIdempotent(
        string environment,
        string enabledSettingKey)
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration(
            new Dictionary<string, string?> { [enabledSettingKey] = "true" });
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        var seeder = CreateSeeder(database.RepositoryRoot, environment, configuration, connectionStringProvider);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        var counts = await ReadCountsAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        Assert.Equal(9, counts.Users);
        Assert.Equal(7, counts.Departments);
        Assert.Equal(2, counts.Projects);
        Assert.Equal(9, counts.ProjectAccess);
        Assert.Equal(1, counts.DisabledUsers);
    }

    [Fact]
    public async Task DevelopmentDataSeeder_DoesNotSeedWhenSettingIsMissing()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        var seeder = CreateSeeder(database.RepositoryRoot, "Development", configuration, connectionStringProvider);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        var counts = await ReadCountsAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        Assert.Equal(0, counts.Users);
        Assert.Equal(0, counts.Projects);
        Assert.Equal(0, counts.ProjectAccess);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    public async Task DevelopmentDataSeeder_FailsWhenExplicitlyEnabledOutsideAllowedEnvironments(string environment)
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration(
            new Dictionary<string, string?> { ["DevelopmentData:SeedEnabled"] = "true" });
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);
        var seeder = CreateSeeder(database.RepositoryRoot, environment, configuration, connectionStringProvider);

        var exception = Assert.Throws<InvalidOperationException>(() => seeder.IsEnabled());
        Assert.Contains("development data seeding cannot be enabled", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static DatabaseMigrationRunner CreateMigrationRunner(
        string repositoryRoot,
        DatabaseConnectionStringProvider connectionStringProvider)
    {
        return new DatabaseMigrationRunner(
            connectionStringProvider,
            new TestWebHostEnvironment(repositoryRoot),
            NullLogger<DatabaseMigrationRunner>.Instance);
    }

    private static DevelopmentIdentitySeeder CreateSeeder(
        string repositoryRoot,
        string environment,
        IConfiguration configuration,
        DatabaseConnectionStringProvider connectionStringProvider)
    {
        return new DevelopmentIdentitySeeder(
            connectionStringProvider,
            configuration,
            new TestWebHostEnvironment(repositoryRoot) { EnvironmentName = environment },
            NullLogger<DevelopmentIdentitySeeder>.Instance);
    }

    private static async Task<DatabaseCounts> ReadCountsAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            select
                (select count(*) from qms_users) as user_count,
                (select count(*) from departments) as department_count,
                (select count(*) from projects) as project_count,
                (select count(*) from user_project_access) as project_access_count,
                (select count(*) from roles) as role_count,
                (select count(*) from permissions) as permission_count,
                (select count(*) from role_permissions) as role_permission_count,
                (select count(*) from qms_users where development_user_key = 'dev-disabled' and is_active = false) as disabled_user_count;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));

        return new DatabaseCounts(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetInt64(7));
    }

    private static async Task AssertCoreConstraintsExistAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            select count(*)
            from pg_constraint
            where conname in (
                'qms_users_development_user_key_key',
                'roles_code_key',
                'permissions_code_key',
                'user_project_access_pkey',
                'user_project_access_user_id_fkey',
                'user_project_access_project_id_fkey'
            );
            """);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        Assert.Equal(6L, value);
    }

    private sealed record DatabaseCounts(
        long Users,
        long Departments,
        long Projects,
        long ProjectAccess,
        long Roles,
        long Permissions,
        long RolePermissions,
        long DisabledUsers);

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private PostgreSqlTestDatabase(string repositoryRoot, string databaseName, IConfiguration baseConfiguration)
        {
            RepositoryRoot = repositoryRoot;
            DatabaseName = databaseName;
            BaseConfiguration = baseConfiguration;
        }

        public string RepositoryRoot { get; }
        public string DatabaseName { get; }
        private IConfiguration BaseConfiguration { get; }

        public static async Task<PostgreSqlTestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var baseConfiguration = BuildBaseDatabaseConfiguration(repositoryRoot);
            var databaseName = $"emi_qms_test_{Guid.NewGuid():N}";
            var adminConnectionString = BuildConnectionString(baseConfiguration, "postgres");

            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"create database {QuoteIdentifier(databaseName)};");
            await command.ExecuteNonQueryAsync(cancellationToken);

            return new PostgreSqlTestDatabase(repositoryRoot, databaseName, baseConfiguration);
        }

        public IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?>? overrides = null)
        {
            var values = BaseConfiguration.AsEnumerable()
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

            values["DATABASE_NAME"] = DatabaseName;

            if (overrides is not null)
            {
                foreach (var item in overrides)
                {
                    values[item.Key] = item.Value;
                }
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        public async ValueTask DisposeAsync()
        {
            var adminConnectionString = BuildConnectionString(BaseConfiguration, "postgres");
            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"drop database if exists {QuoteIdentifier(DatabaseName)} with (force);");
            await command.ExecuteNonQueryAsync();
        }

        private static string QuoteIdentifier(string value)
        {
            return new NpgsqlCommandBuilder().QuoteIdentifier(value);
        }

        private static string BuildConnectionString(IConfiguration configuration, string databaseName)
        {
            var provider = new DatabaseConnectionStringProvider(configuration);
            var configured = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(configured));

            var builder = new NpgsqlConnectionStringBuilder(configured)
            {
                Database = databaseName,
                Pooling = false
            };

            return builder.ConnectionString;
        }

        private static IConfiguration BuildBaseDatabaseConfiguration(string repositoryRoot)
        {
            var values = LoadDotEnv(Path.Combine(repositoryRoot, ".env"));

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .AddEnvironmentVariables()
                .Build();
        }

        private static Dictionary<string, string?> LoadDotEnv(string envPath)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(envPath))
            {
                return values;
            }

            foreach (var rawLine in File.ReadAllLines(envPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                values[parts[0].Trim()] = parts[1].Trim().Trim('"', '\'');
            }

            return values;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "database", "migrations")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find repository root.");
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = DevelopmentFeaturePolicy.TestingEnvironmentName;
        public string ApplicationName { get; set; } = "Emi.Qms.Api.Tests";
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
