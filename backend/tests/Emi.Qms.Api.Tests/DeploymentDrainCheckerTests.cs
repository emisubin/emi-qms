using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.DeploymentMaintenance;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task DeploymentDrainChecker_FailsClosedForSessionsIdentityMaintenanceAndProviderWork()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(databases.RepositoryRoot));
        var privilegeManager = new DatabaseRuntimePrivilegeManager();
        await BootstrapTargetsAsync(new DatabaseRoleBootstrapper(
            databases.Configuration,
            privilegeManager,
            NullLogger<DatabaseRoleBootstrapper>.Instance), ct);

        var runner = new DatabaseMigrationRunner(
            provider,
            catalog,
            privilegeManager,
            databases.Configuration,
            NullLogger<DatabaseMigrationRunner>.Instance);
        await runner.ApplyAndVerifyAsync("DIRECTORY", ct);
        await ApplyCommonBusinessSchemaAsync(databases, catalog, BusinessUnitCodes.Cheongju, ct);
        await runner.ApplyAndVerifyAsync(BusinessUnitCodes.Osan, ct);

        var checker = new DeploymentDrainChecker(
            databases.Configuration,
            provider,
            NullLogger<DeploymentDrainChecker>.Instance);
        var releaseId = Guid.NewGuid();

        AssertSafe(await checker.CheckAsync("DIRECTORY", Guid.Empty, false, ct));
        AssertSafe(await checker.CheckAsync("DIRECTORY", releaseId, true, ct));
        AssertSafe(await checker.CheckAsync(BusinessUnitCodes.Osan, Guid.Empty, false, ct));

        await SetMaintenanceAsync(databases, BusinessUnitCodes.Cheongju, releaseId, "Active", ct);
        AssertSafe(await checker.CheckAsync(BusinessUnitCodes.Cheongju, releaseId, true, ct));

        var roleMismatchValues = new Dictionary<string, string?>(databases.ConfigurationValues)
        {
            ["ConnectionStrings:CheongjuMigration"] = databases.GetBuilder(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Administrator).ConnectionString
        };
        var roleMismatchConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(roleMismatchValues)
            .Build();
        var roleMismatchChecker = new DeploymentDrainChecker(
            roleMismatchConfiguration,
            new DatabaseConnectionStringProvider(roleMismatchConfiguration),
            NullLogger<DeploymentDrainChecker>.Instance);
        Assert.Equal(
            DeploymentDrainChecker.ConfigurationInvalidCode,
            (await roleMismatchChecker.CheckAsync(
                BusinessUnitCodes.Cheongju,
                releaseId,
                true,
                ct)).Code);

        var otherSessionBuilder = databases.GetBuilder(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Runtime);
        otherSessionBuilder.Pooling = false;
        otherSessionBuilder.ApplicationName = "synthetic-drain-other-session";
        await using (var otherSession = new NpgsqlConnection(otherSessionBuilder.ConnectionString))
        {
            await otherSession.OpenAsync(ct);
            Assert.Equal(
                DeploymentDrainChecker.ClientSessionsActiveCode,
                (await checker.CheckAsync(
                    BusinessUnitCodes.Cheongju,
                    releaseId,
                    true,
                    ct)).Code);
        }
        AssertSafe(await checker.CheckAsync(BusinessUnitCodes.Cheongju, releaseId, true, ct));

        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            "update qms_database_identity set business_unit_code = 'OSAN' where singleton = true;",
            ct);
        try
        {
            Assert.Equal(
                DeploymentDrainChecker.DatabaseIdentityMismatchCode,
                (await checker.CheckAsync(
                    BusinessUnitCodes.Cheongju,
                    releaseId,
                    true,
                    ct)).Code);
        }
        finally
        {
            await databases.ExecuteAsync(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                "update qms_database_identity set business_unit_code = 'CHEONGJU' where singleton = true;",
                CancellationToken.None);
        }

        Assert.Equal(
            DeploymentDrainChecker.MaintenanceStateMismatchCode,
            (await checker.CheckAsync(
                BusinessUnitCodes.Cheongju,
                Guid.NewGuid(),
                true,
                ct)).Code);
        Assert.Equal(
            DeploymentDrainChecker.MaintenanceStateMismatchCode,
            (await checker.CheckAsync(
                BusinessUnitCodes.Cheongju,
                Guid.Empty,
                false,
                ct)).Code);
        await SetMaintenanceAsync(databases, BusinessUnitCodes.Cheongju, releaseId, "Delayed", ct);
        AssertSafe(await checker.CheckAsync(BusinessUnitCodes.Cheongju, releaseId, true, ct));

        var processingDeliveryId = Guid.NewGuid();
        var processingClaim = Guid.NewGuid();
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into notification_deliveries (
                id, channel, delivery_type, dedupe_key, status, attempt_count,
                current_generation, generation_attempt_count, claim_token,
                claimed_at_utc, claim_expires_at_utc, claimed_by_instance_id)
            values (
                '{processingDeliveryId:D}', 'Mail', 'ManualTest', 'synthetic-drain-processing',
                'Processing', 1, 1, 1, '{processingClaim:D}', now(), now() + interval '5 minutes',
                'synthetic-drain-worker');
            insert into notification_delivery_attempts (
                delivery_id, attempt_no, generation, claim_token, worker_instance_id,
                claimed_at_utc, lease_expires_at_utc, provider_call_started_at_utc, outcome)
            values (
                '{processingDeliveryId:D}', 1, 1, '{processingClaim:D}', 'synthetic-drain-worker',
                now(), now() + interval '5 minutes', now(), 'Processing');
            """,
            ct);
        Assert.Equal(
            DeploymentDrainChecker.NotificationDeliveryInFlightCode,
            (await checker.CheckAsync(
                BusinessUnitCodes.Cheongju,
                releaseId,
                true,
                ct)).Code);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update notification_delivery_attempts
            set outcome = 'Sent', completed_at_utc = now()
            where delivery_id = '{processingDeliveryId:D}';
            update notification_deliveries
            set status = 'Sent', sent_at_utc = now(), claim_token = null,
                claimed_at_utc = null, claim_expires_at_utc = null,
                claimed_by_instance_id = null
            where id = '{processingDeliveryId:D}';
            """,
            ct);
        AssertSafe(await checker.CheckAsync(BusinessUnitCodes.Cheongju, releaseId, true, ct));

        var uncertainDeliveryId = Guid.NewGuid();
        var uncertainClaim = Guid.NewGuid();
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into notification_deliveries (
                id, channel, delivery_type, dedupe_key, status, attempt_count,
                current_generation, generation_attempt_count, next_attempt_at_utc)
            values (
                '{uncertainDeliveryId:D}', 'Mail', 'ManualTest', 'synthetic-drain-uncertain',
                'Pending', 1, 1, 1, now() + interval '5 minutes');
            insert into notification_delivery_attempts (
                delivery_id, attempt_no, generation, claim_token, worker_instance_id,
                claimed_at_utc, lease_expires_at_utc, provider_call_started_at_utc,
                completed_at_utc, outcome)
            values (
                '{uncertainDeliveryId:D}', 1, 1, '{uncertainClaim:D}', 'synthetic-drain-worker',
                now() - interval '2 minutes', now() - interval '1 minute',
                now() - interval '90 seconds', now() - interval '1 minute',
                'LeaseExpiredAfterProviderCallStarted');
            """,
            ct);
        Assert.Equal(
            DeploymentDrainChecker.NotificationDeliveryResultUncertainCode,
            (await checker.CheckAsync(
                BusinessUnitCodes.Cheongju,
                releaseId,
                true,
                ct)).Code);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"update notification_deliveries set status = 'Failed', next_attempt_at_utc = null where id = '{uncertainDeliveryId:D}';",
            ct);
        Assert.Equal(
            DeploymentDrainChecker.NotificationDeliveryResultUncertainCode,
            (await checker.CheckAsync(
                BusinessUnitCodes.Cheongju,
                releaseId,
                true,
                ct)).Code);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"update notification_deliveries set admin_handling_status = 'Acknowledged', admin_handled_at_utc = now(), admin_handling_note = 'Synthetic administrator acknowledgement.' where id = '{uncertainDeliveryId:D}';",
            ct);
        Assert.Equal(
            DeploymentDrainChecker.NotificationDeliveryResultUncertainCode,
            (await checker.CheckAsync(
                BusinessUnitCodes.Cheongju,
                releaseId,
                true,
                ct)).Code);
        foreach (var terminalStatus in new[] { "Sent", "Suppressed", "Disabled", "DryRunSent" })
        {
            await databases.ExecuteAsync(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"update notification_deliveries set status = '{terminalStatus}' where id = '{uncertainDeliveryId:D}';",
                ct);
            Assert.Equal(
                DeploymentDrainChecker.NotificationDeliveryResultUncertainCode,
                (await checker.CheckAsync(
                    BusinessUnitCodes.Cheongju,
                    releaseId,
                    true,
                    ct)).Code);
        }
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"update notification_delivery_attempts set outcome = 'Sent' where delivery_id = '{uncertainDeliveryId:D}';",
            ct);
        AssertSafe(await checker.CheckAsync(BusinessUnitCodes.Cheongju, releaseId, true, ct));

        var publicationId = Guid.NewGuid();
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"insert into busbar_publication_recovery(product_id) values ('{publicationId:D}');",
            ct);
        Assert.Equal(
            DeploymentDrainChecker.CheongjuProviderResultUncertainCode,
            (await checker.CheckAsync(
                BusinessUnitCodes.Cheongju,
                releaseId,
                true,
                ct)).Code);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"delete from busbar_publication_recovery where product_id = '{publicationId:D}';",
            ct);

        var stateBeforeFinalCheck = await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            "select state || ':' || release_id::text from deployment_maintenance where id = 1;",
            ct);
        AssertSafe(await checker.CheckAsync(BusinessUnitCodes.Cheongju, releaseId, true, ct));
        Assert.Equal(
            stateBeforeFinalCheck,
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                "select state || ':' || release_id::text from deployment_maintenance where id = 1;",
                ct));

        await runner.ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju, ct);
        AssertSafe(await checker.CheckAsync(BusinessUnitCodes.Cheongju, releaseId, true, ct));

        await SetMaintenanceAsync(databases, BusinessUnitCodes.Osan, releaseId, "Active", ct);
        AssertSafe(await checker.CheckAsync(BusinessUnitCodes.Osan, releaseId, true, ct));
    }

    private static async Task ApplyCommonBusinessSchemaAsync(
        IsolationDatabaseSet databases,
        DatabaseMigrationCatalog catalog,
        string targetCode,
        CancellationToken cancellationToken)
    {
        await using var connection = await databases.OpenAsync(
            targetCode,
            BusinessUnitConnectionPurpose.Migration,
            cancellationToken);
        await using (var ledger = connection.CreateCommand())
        {
            ledger.CommandText = """
                create table schema_migrations (
                    version text primary key,
                    applied_at_utc timestamptz not null default now());
                """;
            await ledger.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var migrationFile in catalog.GetCommonMigrationFiles())
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var migration = connection.CreateCommand())
            {
                migration.Transaction = transaction;
                migration.CommandText = await File.ReadAllTextAsync(migrationFile, cancellationToken);
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = "insert into schema_migrations(version) values (@version);";
                record.Parameters.AddWithValue("version", Path.GetFileNameWithoutExtension(migrationFile));
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }

        await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(
            connection,
            databases.BusinessUnits.GetBusiness(targetCode),
            cancellationToken);
    }

    private static Task SetMaintenanceAsync(
        IsolationDatabaseSet databases,
        string targetCode,
        Guid releaseId,
        string state,
        CancellationToken cancellationToken) =>
        databases.ExecuteAsync(
            targetCode,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update deployment_maintenance
            set release_id = '{releaseId:D}', state = '{state}',
                title = 'Synthetic drain check', body = 'Synthetic deployment drain check.',
                starts_at_utc = now() - interval '1 minute',
                expected_ends_at_utc = now() + interval '10 minutes'
            where id = 1;
            """,
            cancellationToken);

    private static void AssertSafe(DeploymentDrainCheckResult result)
    {
        Assert.True(result.IsSafe);
        Assert.Equal(DeploymentDrainChecker.ReadyCode, result.Code);
    }
}
