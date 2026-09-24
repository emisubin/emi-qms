using Emi.Qms.Api.BusinessUnits;
using Npgsql;

namespace Emi.Qms.Api.DeploymentMaintenance;

/// <summary>Cross-instance request/worker drain against the selected business database.</summary>
public sealed class DeploymentMaintenanceLease : IAsyncDisposable
{
    private const long AdvisoryKey = 9070125;
    private readonly List<NpgsqlConnection> connections;
    private readonly List<NpgsqlConnection> lockedConnections;

    private DeploymentMaintenanceLease(List<NpgsqlConnection> connections,List<NpgsqlConnection> lockedConnections)
    { this.connections=connections;this.lockedConnections=lockedConnections; }

    public static async Task<DeploymentMaintenanceLease?> AcquireAsync(
        DatabaseConnectionStringProvider provider,IReadOnlyList<BusinessUnitDatabaseTarget> targets,
        CancellationToken ct)
    {
        var opened=new List<NpgsqlConnection>();
        var locked=new List<NpgsqlConnection>();
        try
        {
            foreach(var target in targets.OrderBy(t=>t.Code,StringComparer.Ordinal))
            {
                var connection=new NpgsqlConnection(provider.GetConnectionString(target));
                await connection.OpenAsync(ct);
                opened.Add(connection);
                await using var command=connection.CreateCommand();
                command.CommandText="select pg_advisory_lock_shared(@key)";
                command.Parameters.AddWithValue("key",AdvisoryKey);
                await command.ExecuteNonQueryAsync(ct);
                locked.Add(connection);
                command.CommandText="select state from deployment_maintenance where id=1";
                var state=(string?)await command.ExecuteScalarAsync(ct);
                if(state is not ("Idle" or "Announced" or "Completed"))
                {
                    await new DeploymentMaintenanceLease(opened,locked).DisposeAsync();
                    return null;
                }
            }
            return new DeploymentMaintenanceLease(opened,locked);
        }
        catch
        {
            await new DeploymentMaintenanceLease(opened,locked).DisposeAsync();
            throw;
        }
    }

    public static async Task<DeploymentMaintenanceLease?> AcquireCurrentAsync(
        DatabaseConnectionStringProvider provider,CancellationToken ct)
    {
        var target=provider.GetCurrentBusinessUnit();
        return target is null ? null : await AcquireAsync(provider,[target],ct);
    }

    public async ValueTask DisposeAsync()
    {
        foreach(var connection in lockedConnections)
        {
            try
            {
                if(connection.State==System.Data.ConnectionState.Open)
                {
                    await using var command=connection.CreateCommand();
                    command.CommandText="select pg_advisory_unlock_shared(@key)";
                    command.Parameters.AddWithValue("key",AdvisoryKey);
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch(NpgsqlException) { /* Connection disposal below also drops a broken session. */ }
        }
        foreach(var connection in connections)
        {
            await connection.DisposeAsync();
        }
    }

    public static async Task AcquireExclusiveAsync(NpgsqlConnection connection,CancellationToken ct)
    {
        await using var command=connection.CreateCommand();
        command.CommandText="select pg_advisory_lock(@key)";
        command.Parameters.AddWithValue("key",AdvisoryKey);
        await command.ExecuteNonQueryAsync(ct);
    }
    public static async Task ReleaseExclusiveAsync(NpgsqlConnection connection)
    {
        await using var command=connection.CreateCommand();
        command.CommandText="select pg_advisory_unlock(@key)";
        command.Parameters.AddWithValue("key",AdvisoryKey);
        await command.ExecuteNonQueryAsync();
    }
}
