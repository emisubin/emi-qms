using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Audit;
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
        var reviewSafe = ReviewSafeMode.IsEnabled(configuration);
        var auditContext = AuditRequestContext.Current;
        if (!reviewSafe && auditContext is null)
        {
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var options = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Options))
        {
            options.Add(builder.Options.Trim());
        }

        if (reviewSafe)
        {
            builder.ApplicationName = ReviewSafeMode.ResolveDatabaseApplicationName(configuration);
            options.Add("-c default_transaction_read_only=on");
        }

        if (auditContext is not null)
        {
            // The audit GUCs below are request-specific. Pooling a connection string that
            // contains a unique request id would create an unbounded number of Npgsql pools.
            // Mutation connections are therefore short-lived; read-only traffic keeps using
            // the stable configured pool.
            builder.Pooling = false;
            options.Add($"-c qms.audit_actor_id={auditContext.ActorUserId:D}");
            options.Add($"-c qms.audit_request_id={auditContext.RequestCorrelationId:D}");
            options.Add($"-c qms.audit_domain={auditContext.Domain}");
            options.Add($"-c qms.audit_action={auditContext.Action}");
            options.Add($"-c qms.audit_route_key={auditContext.RouteKey}");
            if (auditContext.ActualActorUserId is Guid actualActorUserId)
            {
                options.Add($"-c qms.audit_actual_actor_id={actualActorUserId:D}");
            }
            if (auditContext.LoginCorrelationId is Guid loginCorrelationId)
            {
                options.Add($"-c qms.audit_login_id={loginCorrelationId:D}");
            }
        }

        builder.Options = string.Join(' ', options);

        return builder.ConnectionString;
    }
}
