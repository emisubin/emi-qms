using Npgsql;

namespace Emi.Qms.Api;

public sealed class DatabaseHealthChecker(IConfiguration configuration)
{
    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();

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

    private string? GetConnectionString()
    {
        var configured = configuration.GetConnectionString("QmsDatabase");

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var host = configuration["DATABASE_HOST"];
        var port = configuration["DATABASE_PORT"];
        var database = configuration["DATABASE_NAME"];
        var username = configuration["DATABASE_USER"];
        var password = configuration["DATABASE_PASSWORD"];

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(port)
            || string.IsNullOrWhiteSpace(database)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        if (!int.TryParse(port, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var portNumber))
        {
            return null;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = portNumber,
            Database = database,
            Username = username,
            Password = password,
            Pooling = true,
            Timeout = 3
        };

        return builder.ConnectionString;
    }
}
