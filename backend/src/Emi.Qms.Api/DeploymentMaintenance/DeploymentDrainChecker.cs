using System.Data;
using Emi.Qms.Api.BusinessUnits;
using Npgsql;

namespace Emi.Qms.Api.DeploymentMaintenance;

public sealed record DeploymentDrainCheckResult(bool IsSafe, string Code);

/// <summary>
/// Read-only, single-database gate used after application entry points have been stopped and
/// before a deployment job is allowed to change database roles or schema.
/// </summary>
public sealed class DeploymentDrainChecker(
    IConfiguration configuration,
    DatabaseConnectionStringProvider connectionStringProvider,
    ILogger<DeploymentDrainChecker> logger)
{
    public const string ReadyCode = "deployment_drain_ready";
    public const string TargetInvalidCode = "deployment_drain_target_invalid";
    public const string ConfigurationInvalidCode = "deployment_drain_configuration_invalid";
    public const string ReleaseInvalidCode = "deployment_drain_release_invalid";
    public const string DatabaseNameMismatchCode = "deployment_drain_database_name_mismatch";
    public const string MigrationRoleMismatchCode = "deployment_drain_migration_role_mismatch";
    public const string DatabaseIdentityMismatchCode = "deployment_drain_database_identity_mismatch";
    public const string SessionSafetyUnavailableCode = "deployment_drain_session_safety_unavailable";
    public const string ClientSessionsActiveCode = "deployment_drain_client_sessions_active";
    public const string PreparedTransactionsActiveCode = "deployment_drain_prepared_transactions_active";
    public const string MaintenanceTableMissingCode = "deployment_drain_maintenance_table_missing";
    public const string MaintenanceStateMismatchCode = "deployment_drain_maintenance_state_mismatch";
    public const string NotificationDeliveryInFlightCode = "deployment_drain_notification_delivery_in_flight";
    public const string NotificationDeliveryResultUncertainCode = "deployment_drain_notification_delivery_result_uncertain";
    public const string CheongjuProviderInFlightCode = "deployment_drain_cheongju_provider_in_flight";
    public const string CheongjuProviderResultUncertainCode = "deployment_drain_cheongju_provider_result_uncertain";
    public const string CheckUnavailableCode = "deployment_drain_check_unavailable";

    private const int CommandTimeoutSeconds = 5;

    public async Task<DeploymentDrainCheckResult> CheckAsync(
        string targetCode,
        Guid releaseId,
        bool requireMaintenance,
        CancellationToken cancellationToken)
    {
        var target = ResolveTarget(targetCode);
        if (target is null) return Failure(TargetInvalidCode);
        if (requireMaintenance && releaseId == Guid.Empty) return Failure(ReleaseInvalidCode);

        var businessUnits = connectionStringProvider.BusinessUnits;
        if (!businessUnits.Enabled || !businessUnits.IsValid)
        {
            return Failure(ConfigurationInvalidCode);
        }

        var operationErrors = businessUnits.ValidateOperationConnections(
            configuration,
            BusinessUnitConnectionPurpose.Migration,
            [target]);
        if (operationErrors.Count > 0) return Failure(ConfigurationInvalidCode);

        try
        {
            var configuredConnection = connectionStringProvider.GetConnectionString(
                target,
                BusinessUnitConnectionPurpose.Migration);
            var builder = new NpgsqlConnectionStringBuilder(configuredConnection)
            {
                Pooling = false,
                Timeout = CommandTimeoutSeconds,
                CommandTimeout = CommandTimeoutSeconds,
                Enlist = false,
                ApplicationName = $"emi-qms-deployment-drain-{target.Code.ToLowerInvariant()}"
            };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var ct = timeout.Token;

            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                ct);

            await ExecuteAsync(
                connection,
                transaction,
                "set transaction read only; set local statement_timeout = '5s'; set local lock_timeout = '2s'; set local idle_in_transaction_session_timeout = '10s';",
                ct);

            if (!await HasSafeSessionSettingsAsync(connection, transaction, ct))
            {
                return Failure(SessionSafetyUnavailableCode);
            }

            var (databaseName, roleName) = await ReadDatabaseSessionIdentityAsync(
                connection,
                transaction,
                ct);
            if (!string.Equals(databaseName, target.ExpectedDatabaseName, StringComparison.Ordinal))
            {
                return Failure(DatabaseNameMismatchCode);
            }
            if (!string.Equals(roleName, target.MigrationRoleName, StringComparison.Ordinal))
            {
                return Failure(MigrationRoleMismatchCode);
            }
            if (!await IsExpectedIdentityAsync(connection, transaction, target, ct))
            {
                return Failure(DatabaseIdentityMismatchCode);
            }

            if (await ReadCountAsync(connection, transaction, """
                    select count(*)::integer
                    from pg_catalog.pg_stat_activity
                    where datname = current_database()
                      and pid <> pg_backend_pid()
                      and (backend_type = 'client backend' or backend_type is null);
                    """, ct) != 0)
            {
                return Failure(ClientSessionsActiveCode);
            }

            if (await ReadCountAsync(connection, transaction, """
                    select count(*)::integer
                    from pg_catalog.pg_prepared_xacts
                    where database = current_database();
                    """, ct) != 0)
            {
                return Failure(PreparedTransactionsActiveCode);
            }

            if (target.Kind == BusinessUnitDatabaseKind.Business)
            {
                var maintenance = await CheckMaintenanceAsync(
                    connection,
                    transaction,
                    releaseId,
                    requireMaintenance,
                    ct);
                if (maintenance is not null) return maintenance;

                if (await ReadExistsAsync(connection, transaction, """
                        select exists (
                            select 1 from notification_deliveries where status = 'Processing'
                            union all
                            select 1 from notification_delivery_attempts where outcome = 'Processing');
                        """, ct))
                {
                    return Failure(NotificationDeliveryInFlightCode);
                }

                if (await ReadExistsAsync(connection, transaction, """
                        select exists (
                            select 1
                            from notification_delivery_attempts attempt
                            where (attempt.outcome = 'LeaseExpiredAfterProviderCallStarted'
                                   or (attempt.outcome = 'OwnershipLost'
                                       and attempt.provider_call_started_at_utc is not null)));
                        """, ct))
                {
                    return Failure(NotificationDeliveryResultUncertainCode);
                }
            }

            if (string.Equals(target.Code, BusinessUnitCodes.Cheongju, StringComparison.Ordinal))
            {
                if (await ReadExistsAsync(connection, transaction, """
                        select exists (
                            select 1 from busbar_ecount_jobs where state = 'InFlight'
                            union all
                            select 1 from busbar_ecount_attempts where state = 'InFlight');
                        """, ct))
                {
                    return Failure(CheongjuProviderInFlightCode);
                }

                if (await ReadExistsAsync(connection, transaction, """
                        select exists (
                            select 1 from busbar_ecount_jobs where state = 'Unknown'
                            union all
                            select 1 from busbar_ecount_attempts where state = 'Unknown'
                            union all
                            select 1 from busbar_publication_recovery);
                        """, ct))
                {
                    return Failure(CheongjuProviderResultUncertainCode);
                }
            }

            return new DeploymentDrainCheckResult(true, ReadyCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Deployment drain check was unavailable. Target={Target} Code={Code} ExceptionType={ExceptionType}.",
                target.Code,
                CheckUnavailableCode,
                exception.GetType().Name);
            return Failure(CheckUnavailableCode);
        }
    }

    private BusinessUnitDatabaseTarget? ResolveTarget(string targetCode)
    {
        if (string.IsNullOrWhiteSpace(targetCode)) return null;
        var normalized = targetCode.Trim().ToUpperInvariant();
        return connectionStringProvider.BusinessUnits.AllTargets().SingleOrDefault(candidate =>
            string.Equals(candidate.Code, normalized, StringComparison.Ordinal));
    }

    private static async Task<DeploymentDrainCheckResult?> CheckMaintenanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid releaseId,
        bool requireMaintenance,
        CancellationToken cancellationToken)
    {
        var exists = await ReadExistsAsync(connection, transaction,
            "select to_regclass('public.deployment_maintenance') is not null;",
            cancellationToken);
        if (!exists)
        {
            return requireMaintenance ? Failure(MaintenanceTableMissingCode) : null;
        }

        await using var command = CreateCommand(
            connection,
            transaction,
            "select release_id, state from deployment_maintenance where id = 1;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return Failure(MaintenanceStateMismatchCode);
        var currentRelease = reader.IsDBNull(0) ? (Guid?)null : reader.GetGuid(0);
        var state = reader.GetString(1);
        if (await reader.ReadAsync(cancellationToken)) return Failure(MaintenanceStateMismatchCode);

        if (requireMaintenance)
        {
            return currentRelease == releaseId && state is "Active" or "Delayed"
                ? null
                : Failure(MaintenanceStateMismatchCode);
        }

        return state is "Idle" or "Completed"
            ? null
            : Failure(MaintenanceStateMismatchCode);
    }

    private static async Task<bool> HasSafeSessionSettingsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, """
            select current_setting('transaction_read_only') = 'on'
               and current_setting('statement_timeout')::interval <= interval '5 seconds'
               and current_setting('lock_timeout')::interval <= interval '2 seconds'
               and current_setting('idle_in_transaction_session_timeout')::interval <= interval '10 seconds';
            """);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> IsExpectedIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        BusinessUnitDatabaseTarget target,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, """
            select exists (
                select 1
                from public.qms_database_identity
                where singleton = true
                  and database_kind = @database_kind
                  and business_unit_code is not distinct from @business_unit_code
                  and schema_contract = @schema_contract);
            """);
        command.Parameters.AddWithValue(
            "database_kind",
            target.Kind == BusinessUnitDatabaseKind.Directory ? "directory" : "business");
        command.Parameters.Add(new NpgsqlParameter("business_unit_code", NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = target.Kind == BusinessUnitDatabaseKind.Directory ? DBNull.Value : target.Code
        });
        command.Parameters.AddWithValue("schema_contract", target.ExpectedSchemaVersion);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<(string DatabaseName, string RoleName)> ReadDatabaseSessionIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "select current_database(), current_user;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("database_session_identity_missing");
        return (reader.GetString(0), reader.GetString(1));
    }

    private static async Task<int> ReadCountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, sql);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<bool> ReadExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, sql);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql) =>
        new(sql, connection, transaction) { CommandTimeout = CommandTimeoutSeconds };

    private static DeploymentDrainCheckResult Failure(string code) => new(false, code);
}
