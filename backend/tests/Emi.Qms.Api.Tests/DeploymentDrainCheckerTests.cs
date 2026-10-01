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
    public async Task DeploymentDrainChecker_AllowsOnlyTheAcknowledgedHistoricalOsanMailSnapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(databases.RepositoryRoot));
        var privileges = new DatabaseRuntimePrivilegeManager();
        await BootstrapTargetsAsync(new DatabaseRoleBootstrapper(
            databases.Configuration, privileges, NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        var runner = new DatabaseMigrationRunner(provider, catalog, privileges,
            databases.Configuration, NullLogger<DatabaseMigrationRunner>.Instance);
        await runner.ApplyAndVerifyAsync("DIRECTORY", ct);
        await ApplyCommonBusinessSchemaAsync(databases, catalog, BusinessUnitCodes.Cheongju, ct);
        await runner.ApplyAndVerifyAsync(BusinessUnitCodes.Osan, ct);

        var deliveryId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var claim = Guid.NewGuid();
        // Load synthetic historical rows without invoking the current delivery producer.
        // Restore its INSERT guard before exercising the real read-only drain checker.
        var seed = $"""
            begin;
            alter table notification_deliveries disable trigger trg_prevent_osan_external_notification_delivery;
            insert into notification_deliveries (
                id, channel, delivery_type, dedupe_key, status, attempt_count,
                current_generation, generation_attempt_count)
            values ('{deliveryId:D}', 'Mail', 'ManualTest', 'synthetic-acknowledged-mail', 'Failed', 3, 1, 3);
            insert into notification_delivery_attempts (
                id, delivery_id, attempt_no, generation, claim_token, worker_instance_id,
                claimed_at_utc, lease_expires_at_utc, provider_call_started_at_utc,
                completed_at_utc, outcome)
            values ('{attemptId:D}', '{deliveryId:D}', 3, 1, '{claim:D}', 'synthetic-acknowledged-worker',
                '2026-01-02T03:00:00Z', '2026-01-02T03:10:00Z', '2026-01-02T03:01:02.123456Z',
                '2026-01-02T03:11:12.654321Z', 'LeaseExpiredAfterProviderCallStarted');
            alter table notification_deliveries enable trigger trg_prevent_osan_external_notification_delivery;
            commit;
            """;
        await ExecuteAsync(seed);

        // Independently assemble the approved snapshot. Do not call the production matcher.
        string Fingerprint(string channel = "Mail", string status = "Failed")
        {
            var value = string.Join('\n', "osan-mail-deployment-exception-v1",
                databases.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan).ExpectedDatabaseName,
                "OSAN", attemptId.ToString("D"), deliveryId.ToString("D"), "3", "1",
                "LeaseExpiredAfterProviderCallStarted", "2026-01-02T03:01:02.123456Z",
                "2026-01-02T03:11:12.654321Z", channel, status);
            return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(value)));
        }

        async Task<DeploymentDrainCheckResult> CheckAsync(string? fingerprint, string target = "OSAN")
        {
            var values = new Dictionary<string, string?>(databases.ConfigurationValues)
            {
                ["DeploymentDrain:AcceptedHistoricalOsanMailAttemptSha256"] = fingerprint
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            return await new DeploymentDrainChecker(config, new DatabaseConnectionStringProvider(config),
                NullLogger<DeploymentDrainChecker>.Instance).CheckAsync(target, Guid.Empty, false, ct);
        }

        Task ExecuteAsync(string sql) => databases.ExecuteAsync(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration, sql, ct);

        Assert.Equal(DeploymentDrainChecker.NotificationDeliveryResultUncertainCode, (await CheckAsync(null)).Code);
        var accepted = Fingerprint();
        AssertSafe(await CheckAsync(accepted));
        Assert.Equal(DeploymentDrainChecker.ConfigurationInvalidCode, (await CheckAsync("*")).Code);
        Assert.Equal(DeploymentDrainChecker.NotificationDeliveryResultUncertainCode,
            (await CheckAsync(new string('0', 64))).Code);
        Assert.Equal(DeploymentDrainChecker.ConfigurationInvalidCode, (await CheckAsync(accepted, "DIRECTORY")).Code);
        Assert.Equal(DeploymentDrainChecker.ConfigurationInvalidCode, (await CheckAsync(accepted, "CHEONGJU")).Code);

        // A later attempt, even for the same delivery, is never covered by the old snapshot.
        var extraId = Guid.NewGuid();
        await ExecuteAsync($"""
            insert into notification_delivery_attempts (
                id, delivery_id, attempt_no, generation, claim_token, worker_instance_id,
                claimed_at_utc, lease_expires_at_utc, provider_call_started_at_utc, completed_at_utc, outcome)
            values ('{extraId:D}', '{deliveryId:D}', 4, 1, '{Guid.NewGuid():D}', 'synthetic-new-worker',
                now() - interval '2 minutes', now(), now() - interval '1 minute', now(), 'OwnershipLost');
            """);
        Assert.Equal(DeploymentDrainChecker.NotificationDeliveryResultUncertainCode, (await CheckAsync(accepted)).Code);
        await ExecuteAsync($"delete from notification_delivery_attempts where id = '{extraId:D}';");
        await ExecuteAsync("update notification_delivery_attempts set outcome = 'Processing', completed_at_utc = null;");
        Assert.Equal(DeploymentDrainChecker.NotificationDeliveryInFlightCode, (await CheckAsync(accepted)).Code);
        await ExecuteAsync($"delete from notification_deliveries where id = '{deliveryId:D}';" + seed);

        foreach (var change in new[]
        {
            "update notification_delivery_attempts set completed_at_utc = completed_at_utc + interval '1 second'",
            "update notification_delivery_attempts set outcome = 'OwnershipLost'",
            "update notification_delivery_attempts set provider_message_id = 'synthetic-new-evidence'",
            "update notification_deliveries set attempt_count = 4",
            "update notification_deliveries set current_generation = 2",
            "update notification_deliveries set next_attempt_at_utc = now()",
            "update notification_deliveries set provider_message_id = 'synthetic-new-evidence'"
        })
        {
            await ExecuteAsync(change + ";");
            Assert.Equal(DeploymentDrainChecker.NotificationDeliveryResultUncertainCode, (await CheckAsync(accepted)).Code);
            await ExecuteAsync($"delete from notification_deliveries where id = '{deliveryId:D}';" + seed);
        }

        // Even a matching digest cannot exempt a retryable delivery or another channel.
        await ExecuteAsync("update notification_deliveries set status = 'Pending';");
        Assert.Equal(DeploymentDrainChecker.NotificationDeliveryResultUncertainCode,
            (await CheckAsync(Fingerprint(status: "Pending"))).Code);
        await ExecuteAsync("update notification_deliveries set status = 'Failed', channel = 'TeamsActivity';");
        Assert.Equal(DeploymentDrainChecker.NotificationDeliveryResultUncertainCode,
            (await CheckAsync(Fingerprint(channel: "TeamsActivity"))).Code);
        await ExecuteAsync("update notification_deliveries set channel = 'Mail';");
        AssertSafe(await CheckAsync(accepted));

        // Approval does not modify or resolve the original failure, and default checks still reject it.
        Assert.Equal(DeploymentDrainChecker.NotificationDeliveryResultUncertainCode, (await CheckAsync(null)).Code);
        await using var connection = new NpgsqlConnection(databases.GetBuilder(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration).ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand($"""
            select d.status = 'Failed' and a.outcome = 'LeaseExpiredAfterProviderCallStarted'
                and d.attempt_count = 3 and a.attempt_no = 3
                and d.sent_at_utc is null and a.provider_message_id is null
                and a.completed_at_utc = '2026-01-02T03:11:12.654321Z'::timestamptz
            from notification_deliveries d join notification_delivery_attempts a on a.delivery_id = d.id
            where a.id = '{attemptId:D}';
            """, connection);
        Assert.Equal(true, await command.ExecuteScalarAsync(ct));
    }

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
