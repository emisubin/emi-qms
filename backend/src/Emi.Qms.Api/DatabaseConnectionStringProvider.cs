using Emi.Qms.Api.ReviewSafe;
using Npgsql;

namespace Emi.Qms.Api;

public sealed class DatabaseConnectionStringProvider(IConfiguration configuration)
{
    public string? GetConnectionString()
    {
        var configured = configuration.GetConnectionString("QmsDatabase");

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ApplyRuntimeSafety(configured);
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

        if (!int.TryParse(
            port,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var portNumber))
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

        return ApplyRuntimeSafety(builder.ConnectionString);
    }

    private string ApplyRuntimeSafety(string connectionString)
    {
        if (!ReviewSafeMode.IsEnabled(configuration))
        {
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = ReviewSafeMode.ResolveDatabaseApplicationName(configuration),
            Options = "-c default_transaction_read_only=on"
        };

        return builder.ConnectionString;
    }
}
