using System.Net;
using System.Net.Http.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.DeploymentMaintenance;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IntegratedAccessMaintenance_BlockedTargetLeavesAllProfilesAndDirectoryUnchanged(bool multipleMemberships)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await IsolationDatabaseSet.CreateAsync(ct);
        await PrepareIntegratedMaintenanceFixtureAsync(db, multipleMemberships, ct);
        using var factory = QmsWebApplicationFactory.Create(DevelopmentFeaturePolicy.TestingEnvironmentName,
            db.ConfigurationValues, includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();
        var target = await CreateMaintenanceTargetAsync(db, ct);
        var originalGrant = Guid.NewGuid();
        await SendUserAccessUpdateAsync(client, target, originalGrant, 0, [Profile(BusinessUnitCodes.Cheongju, "sales")]);
        var baseline = await MaintenanceTargetSnapshotAsync(db, target, ct);
        foreach (var blockedUnit in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        foreach (var state in new[] { "Active", "Delayed", "Failed" })
        {
            await SetMaintenanceAuditStateAsync(db, blockedUnit, state, ct);
            using var request = CreateUserAccessUpdateRequest(target, Guid.NewGuid(), 1, [Profile(BusinessUnitCodes.Osan, "quality")]);
            // Keep a literal common route; a helper must not conceal an alias bug.
            request.RequestUri = new Uri($"/access/api/admin/user-access/users/{target:D}/access", UriKind.Relative);
            using var response = await client.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<BusinessUnitAccessAdministrationErrorResponse>(ct);
            Assert.Equal("release_maintenance", error!.ErrorCode);
            Assert.Equal(baseline, await MaintenanceTargetSnapshotAsync(db, target, ct));
            await SetMaintenanceAuditStateAsync(db, blockedUnit, "Idle", ct);
        }

        var operation = Guid.NewGuid();
        var moved = await SendUserAccessUpdateAsync(client, target, operation, 1, [Profile(BusinessUnitCodes.Osan, "quality")]);
        Assert.True(moved.Changed);
        Assert.Equal(2, moved.AccessVersion);
        var replay = await SendUserAccessUpdateAsync(client, target, operation, 1, [Profile(BusinessUnitCodes.Osan, "quality")]);
        Assert.False(replay.Changed);
        Assert.Equal(2, replay.AccessVersion);
        var afterMove = await MaintenanceTargetSnapshotAsync(db, target, ct);
        var oldReplay = await SendUserAccessUpdateAsync(client, target, originalGrant, 0, [Profile(BusinessUnitCodes.Cheongju, "sales")]);
        Assert.False(oldReplay.Changed);
        Assert.Equal(2, oldReplay.AccessVersion);
        Assert.Equal(afterMove, await MaintenanceTargetSnapshotAsync(db, target, ct));
    }

    [Fact]
    public async Task IntegratedAccessMaintenance_HoldsOneLeasePerTargetThroughDirectoryPublish()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var ct = timeout.Token;
        await using var db = await IsolationDatabaseSet.CreateAsync(ct);
        // A single membership auto-selects C even on /access. Middleware must not take a second lease.
        await PrepareIntegratedMaintenanceFixtureAsync(db, false, ct);
        using var factory = QmsWebApplicationFactory.Create(DevelopmentFeaturePolicy.TestingEnvironmentName,
            db.ConfigurationValues, includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();
        var target = await CreateMaintenanceTargetAsync(db, ct);
        const long publishBarrier = 9070199;
        await db.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            create function test_pause_access_publish() returns trigger language plpgsql as $$
            begin
                if new.user_id = '{target:D}' and new.access_version > old.access_version then
                    perform pg_advisory_xact_lock({publishBarrier});
                end if;
                return new;
            end $$;
            create trigger test_pause_access_publish before update on directory_identities
                for each row execute function test_pause_access_publish();
            """, ct);
        await using var barrier = await db.OpenAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, ct);
        await using var barrierCommand = barrier.CreateCommand();
        barrierCommand.CommandText = $"select pg_advisory_lock({publishBarrier})";
        await barrierCommand.ExecuteNonQueryAsync(ct);
        await using var maintenance = await db.OpenAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, ct);
        Task<HttpResponseMessage>? pending = null;
        Task? exclusive = null;
        try
        {
            using var request = CreateUserAccessUpdateRequest(target, Guid.NewGuid(), 0,
                [Profile(BusinessUnitCodes.Cheongju, "sales"), Profile(BusinessUnitCodes.Osan, "quality")], isOverallAdministrator: true);
            pending = client.SendAsync(request, ct);
            await WaitForAuditLockAsync(db, "DIRECTORY", publishBarrier, granted: false, ct);
            // Local commits happened, but Directory publication is still waiting on the synthetic barrier.
            foreach (var code in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
            {
                Assert.Equal(1L, await db.ReadScalarAsync<long>(code, BusinessUnitConnectionPurpose.Migration,
                    $"select count(*) from qms_users where id='{target:D}' and is_active", ct));
                Assert.Equal(1L, await db.ReadScalarAsync<long>(code, BusinessUnitConnectionPurpose.Migration,
                    "select count(*) from pg_locks where locktype='advisory' and database=(select oid from pg_database where datname=current_database()) and classid=0 and objid=9070125 and mode='ShareLock' and granted", ct));
            }
            Assert.Equal(0L, await db.ReadScalarAsync<long>("DIRECTORY", BusinessUnitConnectionPurpose.Migration,
                $"select access_version from directory_identities where user_id='{target:D}'", ct));
            exclusive = DeploymentMaintenanceLease.AcquireExclusiveAsync(maintenance, ct);
            await WaitForAuditLockAsync(db, BusinessUnitCodes.Osan, 9070125, granted: false, ct);
            Assert.False(exclusive.IsCompleted);
            barrierCommand.CommandText = $"select pg_advisory_unlock({publishBarrier})";
            await barrierCommand.ExecuteNonQueryAsync(ct);
            using var response = await pending;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await exclusive;
            Assert.Equal(1L, await db.ReadScalarAsync<long>("DIRECTORY", BusinessUnitConnectionPurpose.Migration,
                $"select access_version from directory_identities where user_id='{target:D}'", ct));
        }
        finally
        {
            // Release the synthetic barrier and cancel waiters even when an assertion fails.
            barrierCommand.CommandText = $"select pg_advisory_unlock({publishBarrier})";
            await barrierCommand.ExecuteNonQueryAsync(CancellationToken.None);
            await timeout.CancelAsync();
            if (pending is not null) { try { (await pending).Dispose(); } catch (OperationCanceledException) { } }
            if (exclusive is not null) { try { await exclusive; } catch (OperationCanceledException) { } }
            await DeploymentMaintenanceLease.ReleaseExclusiveAsync(maintenance);
        }
    }

    private static async Task PrepareIntegratedMaintenanceFixtureAsync(IsolationDatabaseSet db, bool multipleMemberships, CancellationToken ct)
    {
        var provider = new DatabaseConnectionStringProvider(db.Configuration);
        var environment = new TestEnvironment(db.RepositoryRoot);
        var catalog = new DatabaseMigrationCatalog(environment);
        var inspector = new MigrationLedgerInspector(catalog);
        await BootstrapTargetsAsync(new DatabaseRoleBootstrapper(db.Configuration, new DatabaseRuntimePrivilegeManager(), NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        await MigrateTargetsAsync(new DatabaseMigrationRunner(provider, catalog, new DatabaseRuntimePrivilegeManager(), db.Configuration, NullLogger<DatabaseMigrationRunner>.Instance), ct);
        await new DevelopmentIdentitySeeder(provider, db.Configuration, environment, NullLogger<DevelopmentIdentitySeeder>.Instance, inspector).SeedAsync(ct);
        db.ConfigurationValues["DevelopmentData:SeedEnabled"] = "false";
        await new BusinessUnitMembershipBackfillRunner(provider, db.Configuration, NullLogger<BusinessUnitMembershipBackfillRunner>.Instance,
            inspector, new BusinessUnitDirectoryMigrationCatalog(catalog)).ApplyAsync(ct);
        await PrepareDirectoryAndBusinessFixturesAsync(db);
        if (!multipleMemberships)
            await db.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration,
                $"delete from directory_business_unit_memberships where user_id='{AdminUserId:D}' and business_unit_code='OSAN'", ct);
    }

    private static async Task<Guid> CreateMaintenanceTargetAsync(IsolationDatabaseSet db, CancellationToken ct)
    {
        var target = Guid.NewGuid();
        await db.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            insert into directory_identities(user_id,auth_provider,external_subject,display_name,email,is_active)
            values('{target:D}','EntraId','maintenance-{target:N}','Synthetic Maintenance Target','maintenance@example.invalid',true);
            """, ct);
        return target;
    }

    private static Task SetMaintenanceAuditStateAsync(IsolationDatabaseSet db, string code, string state, CancellationToken ct) =>
        db.ExecuteAsync(code, BusinessUnitConnectionPurpose.Migration, $"""
            update deployment_maintenance set state='{state}',release_id=gen_random_uuid(),title='Synthetic test',body='Synthetic test',
                starts_at_utc=now(),expected_ends_at_utc=now()+interval '1 hour' where id=1;
            """, ct);

    private static async Task<string> MaintenanceTargetSnapshotAsync(IsolationDatabaseSet db, Guid target, CancellationToken ct)
    {
        var results = new List<string>();
        foreach (var code in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
            results.Add(await db.ReadScalarAsync<string>(code, BusinessUnitConnectionPurpose.Migration, $"""
                select json_build_object('user',(select row_to_json(u) from qms_users u where id='{target:D}'),
                    'roles',(select coalesce(json_agg(ur order by role_id),'[]') from user_roles ur where user_id='{target:D}'),
                    'audit_count',(select count(*) from audit_events where target_key='{target:D}'))::text;
                """, ct));
        results.Add(await db.ReadScalarAsync<string>("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            select json_build_object('user',(select row_to_json(i) from directory_identities i where user_id='{target:D}'),
                'memberships',(select coalesce(json_agg(m order by business_unit_code),'[]') from directory_business_unit_memberships m where user_id='{target:D}'),
                'operations',(select coalesce(json_agg(o order by operation_id),'[]') from directory_user_access_operations o where target_user_id='{target:D}'))::text;
            """, ct));
        return string.Join('\n', results);
    }

    private static async Task WaitForAuditLockAsync(IsolationDatabaseSet db, string code, long key, bool granted, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await db.ReadScalarAsync<bool>(code, BusinessUnitConnectionPurpose.Migration,
                $"select exists(select 1 from pg_locks where locktype='advisory' and database=(select oid from pg_database where datname=current_database()) and classid=0 and objid={key} and granted={granted.ToString().ToLowerInvariant()})", ct)) return;
            await Task.Delay(25, ct);
        }
        throw new TimeoutException("Synthetic access/maintenance request did not reach its lock barrier.");
    }
}
