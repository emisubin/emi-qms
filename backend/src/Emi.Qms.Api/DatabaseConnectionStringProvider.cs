using Emi.Qms.Api.Audit;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Collections.Concurrent;

namespace Emi.Qms.Api;

public sealed class DatabaseConnectionStringProvider : IDisposable, IAsyncDisposable
{
    private readonly IConfiguration configuration;
    private readonly IHttpContextAccessor? httpContextAccessor;
    private readonly ConcurrentDictionary<string, Lazy<NpgsqlDataSource>> runtimeSources = new(StringComparer.Ordinal);
    private bool disposed;

    // Stable runtime connections share one pool per configuration. Audited mutations
    // carry request-specific startup settings and must never enter a shared pool.
    public RuntimeDataSourceLease RentDataSource(string connectionString)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (!settings.Pooling)
            return new(NpgsqlDataSource.Create(connectionString), ownsSource: true);
        var source = runtimeSources.GetOrAdd(settings.ConnectionString,
            value => new Lazy<NpgsqlDataSource>(() => NpgsqlDataSource.Create(value))).Value;
        return new(source, ownsSource: false);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        foreach (var source in runtimeSources.Values)
            if (source.IsValueCreated) await source.Value.DisposeAsync();
        runtimeSources.Clear();
    }

    public void Dispose()
    {
        disposed = true;
        foreach (var source in runtimeSources.Values)
            if (source.IsValueCreated) source.Value.Dispose();
        runtimeSources.Clear();
    }

    public DatabaseConnectionStringProvider(IConfiguration configuration)
        : this(configuration, null)
    {
    }

    [ActivatorUtilitiesConstructor]
    public DatabaseConnectionStringProvider(
        IConfiguration configuration,
        IHttpContextAccessor? httpContextAccessor)
    {
        this.configuration = configuration;
        this.httpContextAccessor = httpContextAccessor;
        BusinessUnits = BusinessUnitConfiguration.Read(configuration);
    }

    public BusinessUnitConfiguration BusinessUnits { get; }

    public string? GetConnectionString()
    {
        if (!BusinessUnits.Enabled)
        {
            return GetLegacyConnectionString();
        }

        var context = BusinessUnitRequestContextFeature.Get(httpContextAccessor?.HttpContext);
        if (context?.IsSelected != true || context.Target is null)
        {
            throw new BusinessUnitContextUnavailableException(context?.Reason ?? "business_unit_context_missing");
        }

        return GetConnectionString(context.Target, BusinessUnitConnectionPurpose.Runtime);
    }

    public string GetConnectionString(
        BusinessUnitDatabaseTarget target,
        BusinessUnitConnectionPurpose purpose = BusinessUnitConnectionPurpose.Runtime)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!BusinessUnits.Enabled
            && target.IsLegacy
            && purpose == BusinessUnitConnectionPurpose.Runtime)
        {
            return GetLegacyConnectionString()
                ?? throw new InvalidOperationException("The requested database connection is not configured.");
        }
        if (BusinessUnits.Enabled)
        {
            BusinessUnits.ThrowIfInvalid();
            var configuredTarget = target.Kind == BusinessUnitDatabaseKind.Directory
                ? BusinessUnits.Directory
                : BusinessUnits.Businesses.SingleOrDefault(candidate =>
                    string.Equals(candidate.Code, target.Code, StringComparison.Ordinal));
            if (configuredTarget is null || configuredTarget != target)
            {
                throw new BusinessUnitContextUnavailableException("business_unit_target_not_configured");
            }
        }

        var connectionName = purpose switch
        {
            BusinessUnitConnectionPurpose.Runtime => target.RuntimeConnectionName,
            BusinessUnitConnectionPurpose.Migration => target.MigrationConnectionName,
            BusinessUnitConnectionPurpose.Administrator => target.AdministratorConnectionName,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
        var configured = configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("The requested database connection is not configured.");
        }

        return purpose == BusinessUnitConnectionPurpose.Runtime
            ? ApplyRuntimeSafety(configured, target)
            : configured;
    }

    public BusinessUnitDatabaseTarget? GetCurrentBusinessUnit()
    {
        if (!BusinessUnits.Enabled)
        {
            return BusinessUnits.Businesses.Single();
        }

        return BusinessUnitRequestContextFeature.Get(httpContextAccessor?.HttpContext)?.Target;
    }

    public bool ExternalNotificationsEnabled(BusinessUnitDatabaseTarget? explicitTarget = null)
    {
        var target = explicitTarget ?? GetCurrentBusinessUnit();
        return target?.ExternalNotificationsEnabled == true;
    }

    internal async Task<IReadOnlyList<Guid>> ReadOverallAdministratorIdsAsync(CancellationToken ct)
    {
        if (!BusinessUnits.Enabled || BusinessUnits.Directory is not { } directory) return [];
        await using var source = RentDataSource(GetConnectionString(directory));
        await using var command = source.CreateCommand("""
            select distinct a.user_id from directory_overall_administrators a
            join directory_identities i on i.user_id=a.user_id where a.is_active and i.is_active
            """);
        var result = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0));
        return result;
    }

    private string? GetLegacyConnectionString()
    {
        var configured = configuration.GetConnectionString("QmsDatabase");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ApplyRuntimeSafety(configured, BusinessUnits.Businesses.Single());
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
            || string.IsNullOrWhiteSpace(password)
            || !int.TryParse(
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
        return ApplyRuntimeSafety(builder.ConnectionString, BusinessUnits.Businesses.Single());
    }

    private string ApplyRuntimeSafety(string connectionString, BusinessUnitDatabaseTarget target)
    {
        var reviewSafe = ReviewSafeMode.IsEnabled(configuration);
        var auditContext = AuditRequestContext.Current;
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var options = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Options)) options.Add(builder.Options.Trim());

        if (BusinessUnits.Enabled)
        {
            builder.ApplicationName = $"emi-pms-{target.Code.ToLowerInvariant()}";
            options.Add($"-c qms.business_unit={target.Code}");
        }

        if (reviewSafe)
        {
            builder.ApplicationName = BusinessUnits.Enabled
                ? $"{ReviewSafeMode.ResolveDatabaseApplicationName(configuration)}-{target.Code.ToLowerInvariant()}"
                : ReviewSafeMode.ResolveDatabaseApplicationName(configuration);
            options.Add("-c default_transaction_read_only=on");
        }

        if (auditContext is not null)
        {
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
