using Npgsql;

namespace Emi.Qms.Api;

public sealed class DatabaseHealthChecker(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DatabaseHealthResult(false, "not_configured");
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var command = dataSource.CreateCommand("select 1");
            var value = await command.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(value) == 1
                ? new DatabaseHealthResult(true, "reachable")
                : new DatabaseHealthResult(false, "unexpected_response");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new DatabaseHealthResult(false, "unreachable");
        }
    }
}
