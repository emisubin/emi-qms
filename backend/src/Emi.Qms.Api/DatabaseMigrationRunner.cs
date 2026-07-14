using Emi.Qms.Api.ReviewSafe;
using Npgsql;

namespace Emi.Qms.Api;

public sealed class DatabaseMigrationRunner(
    DatabaseConnectionStringProvider connectionStringProvider,
    DatabaseMigrationCatalog migrationCatalog,
    IConfiguration configuration,
    ILogger<DatabaseMigrationRunner> logger)
{
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (ReviewSafeMode.IsEnabled(configuration))
        {
            throw new InvalidOperationException("Database migrations are disabled in review-safe UAT mode.");
        }

        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }

        var migrationFiles = migrationCatalog.GetMigrationFiles();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                create table if not exists schema_migrations (
                    version text primary key,
                    applied_at_utc timestamptz not null default now()
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var migrationFile in migrationFiles)
        {
            var version = Path.GetFileNameWithoutExtension(migrationFile);

            if (await IsMigrationAppliedAsync(connection, version, cancellationToken))
            {
                continue;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var migrationCommand = connection.CreateCommand())
            {
                migrationCommand.Transaction = transaction;
                migrationCommand.CommandText = await File.ReadAllTextAsync(migrationFile, cancellationToken);
                await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var recordCommand = connection.CreateCommand())
            {
                recordCommand.Transaction = transaction;
                recordCommand.CommandText = """
                    insert into schema_migrations (version)
                    values (@version);
                    """;
                recordCommand.Parameters.AddWithValue("version", version);
                await recordCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Applied database migration {MigrationVersion}.", version);
        }
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        NpgsqlConnection connection,
        string version,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists (select 1 from schema_migrations where version = @version);";
        command.Parameters.AddWithValue("version", version);
        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value is bool isApplied && isApplied;
    }

}
