using System.Net;
using System.Security.Claims;
using Emi.Qms.Api.Audit;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.DeploymentMaintenance;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task AuthenticationMaintenance_ReadsExistingIdentityWithoutWritingAnyDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await IsolationDatabaseSet.CreateAsync(ct);
        await PrepareIntegratedMaintenanceFixtureAsync(db, true, ct);
        var userId = await CreateMaintenanceTargetAsync(db, ct);
        var unprovisionedId = await CreateMaintenanceTargetAsync(db, ct);
        var subject = $"maintenance-{userId:N}";
        await db.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration,
            $"insert into directory_business_unit_memberships(user_id,business_unit_code,is_active) values('{userId:D}','OSAN',true)", ct);
        await db.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration,
            $"insert into directory_business_unit_memberships(user_id,business_unit_code,is_active) values('{unprovisionedId:D}','OSAN',true)", ct);
        var accessor = new HttpContextAccessor();
        await using var provider = new DatabaseConnectionStringProvider(db.Configuration, accessor);
        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(db.RepositoryRoot));
        var directoryCatalog = new BusinessUnitDirectoryMigrationCatalog(catalog);
        var directoryStore = new BusinessUnitDirectoryStore(provider, directoryCatalog);
        var resolver = new BusinessUnitResolver(provider, directoryStore,
            new BusinessUnitDatabaseBoundaryValidator(provider, new MigrationLedgerInspector(catalog), directoryCatalog));

        Task<ClaimsPrincipal> Authenticate(string externalSubject, string name, string method)
        {
            accessor.HttpContext = new DefaultHttpContext();
            accessor.HttpContext.Request.Method = method;
            accessor.HttpContext.Request.Path = "/api/me";
            accessor.HttpContext.Features.Set(new BusinessUnitRoute(BusinessUnitCodes.Osan, false));
            // A new scoped store, matching the request lifetime in the real host.
            var transformation = new EntraClaimsTransformation(new DbIdentityStore(provider, db.Configuration),
                new InMemoryIdentityStore(), db.Configuration, new TestEnvironment(db.RepositoryRoot),
                accessor, resolver, directoryStore);
            return transformation.TransformAsync(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("oid", externalSubject), new Claim("name", name),
                 new Claim("preferred_username", "maintenance@example.invalid")], QmsAuthenticationSchemes.EntraBearer)));
        }

        Assert.Equal(userId.ToString("D"), (await Authenticate(subject, "Before freeze", "GET"))
            .FindFirst(QmsClaimTypes.UserId)?.Value);
        var snapshot = await MaintenanceTargetSnapshotAsync(db, userId, ct);
        foreach (var state in new[] { "Active", "Delayed", "Failed" })
        {
            await SetMaintenanceAuditStateAsync(db, BusinessUnitCodes.Osan, state, ct);
            foreach (var method in new[] { "GET", "POST" })
            {
                var principal = await Authenticate(subject, "Must not be saved", method);
                Assert.Equal(userId.ToString("D"), principal.FindFirst(QmsClaimTypes.UserId)?.Value);
                Assert.Equal(snapshot, await MaintenanceTargetSnapshotAsync(db, userId, ct));
                var context = accessor.HttpContext!;
                context.User = principal;
                context.Response.Body = new MemoryStream();
                var nextCalled = false;
                await new DeploymentMaintenanceMiddleware(_ => { nextCalled = true; return Task.CompletedTask; })
                    .InvokeAsync(context, provider);
                Assert.Equal(method == "GET", nextCalled);
                if (method == "POST") Assert.Equal(503, context.Response.StatusCode);
            }
            var newSubject = $"new-during-{state}";
            await Authenticate(newSubject, "Unregistered during freeze", "GET");
            Assert.Equal(0L, await db.ReadScalarAsync<long>("DIRECTORY", BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_identities where external_subject='{newSubject}'", ct));
            var pending = await Authenticate($"maintenance-{unprovisionedId:N}", "Synthetic Maintenance Target", "GET");
            Assert.Equal(BusinessUnitAccessStatuses.LocalProfilePending,
                pending.FindFirst(QmsClaimTypes.BusinessUnitAccessStatus)?.Value);
            Assert.Equal(0L, await db.ReadScalarAsync<long>(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from qms_users where id='{unprovisionedId:D}'", ct));
        }
        await SetMaintenanceAuditStateAsync(db, BusinessUnitCodes.Osan, "Idle", ct);
        await Authenticate(subject, "After freeze", "GET");
        Assert.NotEqual(snapshot, await MaintenanceTargetSnapshotAsync(db, userId, ct));
        Assert.Equal("After freeze", await db.ReadScalarAsync<string>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration, $"select display_name from qms_users where id='{userId:D}'", ct));

        // A drain racing an identity synchronization must wait for its commit, in
        // both the Directory and the selected business database.
        foreach (var location in new[] { "DIRECTORY", BusinessUnitCodes.Osan })
        {
            const long barrierKey = 9070299;
            var table = location == "DIRECTORY" ? "directory_identities" : "qms_users";
            var key = location == "DIRECTORY" ? "user_id" : "id";
            await db.ExecuteAsync(location, BusinessUnitConnectionPurpose.Migration, $"""
                create function test_pause_identity_sync() returns trigger language plpgsql as $$
                begin
                    if new.{key}='{userId:D}' then perform pg_advisory_xact_lock({barrierKey}); end if;
                    return new;
                end $$;
                create trigger test_pause_identity_sync before update on {table}
                for each row execute function test_pause_identity_sync();
                """, ct);
            await using var barrier = await db.OpenAsync(location, BusinessUnitConnectionPurpose.Migration, ct);
            await using var barrierCommand = barrier.CreateCommand();
            barrierCommand.CommandText = $"select pg_advisory_lock({barrierKey})";
            await barrierCommand.ExecuteNonQueryAsync(ct);
            await using var drain = await db.OpenAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, ct);
            var pending = Authenticate(subject, $"Sync {location}", "GET");
            try
            {
                await WaitForAuditLockAsync(db, location, barrierKey, false, ct);
                var exclusive = DeploymentMaintenanceLease.AcquireExclusiveAsync(drain, ct);
                await WaitForAuditLockAsync(db, BusinessUnitCodes.Osan, 9070125, false, ct);
                Assert.False(exclusive.IsCompleted);
                barrierCommand.CommandText = $"select pg_advisory_unlock({barrierKey})";
                await barrierCommand.ExecuteNonQueryAsync(ct);
                await exclusive;
                await DeploymentMaintenanceLease.ReleaseExclusiveAsync(drain);
                Assert.Equal(userId.ToString("D"), (await pending).FindFirst(QmsClaimTypes.UserId)?.Value);
            }
            finally
            {
                barrierCommand.CommandText = $"select pg_advisory_unlock({barrierKey})";
                await barrierCommand.ExecuteNonQueryAsync(ct);
                await DeploymentMaintenanceLease.ReleaseExclusiveAsync(drain);
                await pending;
                await db.ExecuteAsync(location, BusinessUnitConnectionPurpose.Migration,
                    $"drop trigger test_pause_identity_sync on {table}; drop function test_pause_identity_sync();", ct);
            }
        }
    }

    [Fact]
    public async Task RuntimePools_ReusePerDatabaseAndKeepAuditAndReadOnlySettingsIsolated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await IsolationDatabaseSet.CreateAsync(ct);
        await PrepareIntegratedMaintenanceFixtureAsync(db, true, ct);
        var poolValues = new Dictionary<string, string?>(db.ConfigurationValues);
        foreach (var target in db.BusinessUnits.AllTargets())
        {
            var key = $"ConnectionStrings:{target.RuntimeConnectionName}";
            poolValues[key] = new NpgsqlConnectionStringBuilder(poolValues[key])
                { Pooling = true, MaxPoolSize = 2 }.ConnectionString;
        }
        await using var provider = new DatabaseConnectionStringProvider(new ConfigurationBuilder().AddInMemoryCollection(poolValues).Build());
        async Task<(int Pid, string Database, string Actor)> ReadSession(BusinessUnitDatabaseTarget target)
        {
            await using var lease = provider.RentDataSource(provider.GetConnectionString(target));
            await using var connection = await lease.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "select pg_backend_pid(),current_database(),coalesce(current_setting('qms.audit_actor_id',true),'')";
            await using var reader = await command.ExecuteReaderAsync(ct);
            Assert.True(await reader.ReadAsync(ct));
            return (reader.GetInt32(0), reader.GetString(1), reader.GetString(2));
        }
        var osan = db.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan);
        var cheongju = db.BusinessUnits.GetBusiness(BusinessUnitCodes.Cheongju);
        var first = await ReadSession(osan);
        Assert.Equal(first, await ReadSession(osan)); // disposing a lease does not dispose the pool
        var other = await ReadSession(cheongju);
        Assert.NotEqual(first.Database, other.Database);
        Assert.NotEqual(first.Pid, other.Pid);
        var directory = await ReadSession(db.BusinessUnits.Directory!);
        Assert.NotEqual(first.Database, directory.Database);
        Assert.NotEqual(other.Database, directory.Database);
        foreach (var actor in new[] { AdminUserId, SalesUserId })
        {
            using var audit = AuditRequestContext.Push(new(actor, null, Guid.NewGuid(), null, "Test", "Update", "test"));
            var session = await ReadSession(osan);
            Assert.Equal(actor.ToString("D"), session.Actor);
            Assert.NotEqual(first.Pid, session.Pid);
        }
        Assert.Equal(first, await ReadSession(osan));
        var values = new Dictionary<string, string?>(poolValues) { ["ReviewSafe:Enabled"] = "true" };
        await using var readOnlyProvider = new DatabaseConnectionStringProvider(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        await using var readOnlyLease = readOnlyProvider.RentDataSource(readOnlyProvider.GetConnectionString(osan));
        await using var readOnly = await readOnlyLease.OpenConnectionAsync(ct);
        await using var write = readOnly.CreateCommand();
        write.CommandText = "update qms_users set display_name=display_name where false";
        Assert.Equal(PostgresErrorCodes.ReadOnlySqlTransaction,
            (await Assert.ThrowsAsync<PostgresException>(() => write.ExecuteNonQueryAsync(ct))).SqlState);
        var function = await db.ReadScalarAsync<string>(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select pg_get_functiondef('qms_audit_capture_row_change()'::regprocedure)", ct);
        Assert.DoesNotContain("g2_daily_metrics", function, StringComparison.Ordinal);
        Assert.DoesNotContain("pending_issues", function, StringComparison.Ordinal);
        Assert.Contains("notice_posts.body_format", function, StringComparison.Ordinal);
    }
}

public sealed class CreatedBusinessLocationTests
{
    [Theory]
    [InlineData("/cheongju/api/projects/item", "/cheongju/api/projects/item")]
    [InlineData("/osan/api/osan/projects/item", "/osan/api/osan/projects/item")]
    [InlineData("/access/api/admin/user-access/item", "/access/api/admin/user-access/item")]
    [InlineData("/api/projects/item", "/api/projects/item")]
    public async Task CreationLocationCanBeFollowedThroughTheSameFixedRoute(string path, string expected)
    {
        // Run the actual routing middleware and HTTP response-start callback.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new DatabaseConnectionStringProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["BusinessUnits:Enabled"] = (!path.StartsWith("/api/", StringComparison.Ordinal)).ToString() }).Build()));
        await using var app = builder.Build();
        app.UseMiddleware<BusinessUnitRouteMiddleware>();
        app.Run(async context =>
        {
            if (context.Request.Method == "POST")
            {
                context.Response.StatusCode = 201;
                context.Response.Headers.Location = context.Request.Path.ToString();
            }
            await context.Response.WriteAsync(context.Request.Path.ToString());
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var created = await client.PostAsync(path, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(expected, created.Headers.Location?.ToString());
        using var followed = await client.GetAsync(created.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
    }
}
