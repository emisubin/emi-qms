using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class G2MaterialCatalogProvisionerTests
{
    private static readonly Guid ReleaseId = Guid.Parse("71000000-0000-0000-0000-000000000001");

    [Fact]
    public void ApprovedTemplateProducesFixedMetadataAndMatchesPrivateReferenceWhenProvided()
    {
        var actual = G2MaterialCatalogProvisioner.ReadApprovedTemplateCatalog();
        Assert.Equal(92, actual.Count);
        Assert.Equal(Enumerable.Range(1, 92), actual.Select(item => item.Id));
        Assert.Equal(Enumerable.Range(4, 92), actual.Select(item => item.SourceRow));
        Assert.All(actual, item =>
        {
            Assert.Contains(item.Category, new[] { "사급", "도급", "부자재" });
            Assert.False(string.IsNullOrWhiteSpace(item.Code));
            Assert.True(item.SetQuantity > 0);
        });
        Assert.Equal(47, actual.Sum(item => item.ImageKeys?.Length ?? 0));
        Assert.Equal("item-01-1.png", Assert.Single(actual[0].ImageKeys!));
        Assert.Equal("#FFC000", actual[30].CellFills!["E"]);
        Assert.Equal("#FFFF00", actual[35].CellFills!["F"]);
        Assert.Equal("#D9D9D9", actual[0].CellFills!["R"]);

        var referencePath = Environment.GetEnvironmentVariable("G2_PRIVATE_CATALOG_REFERENCE");
        if (string.IsNullOrWhiteSpace(referencePath)) return;
        var reference = JsonSerializer.Deserialize<G2MaterialCatalog[]>(
            File.ReadAllText(referencePath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(reference);
        Assert.Equal(CatalogDigest(reference), CatalogDigest(actual));
    }

    [Fact]
    public async Task ProvisioningRejectsUnsafeContextsAndInsertsOnceBehindExclusiveGate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        var migrationCatalog = DatabaseMigrationCatalog.FromPaths(
            Path.Combine(db.RepositoryRoot, "database", "migrations"),
            Path.Combine(db.RepositoryRoot, "database", "business-migrations"));
        var baseConfiguration = Configuration(db, BusinessUnitCodes.Cheongju, ReleaseId, reviewSafe: false);
        await BusinessUnitIsolationTests.BootstrapTargetsAsync(
            new DatabaseRoleBootstrapper(baseConfiguration, new DatabaseRuntimePrivilegeManager(),
                NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        await using (var migrationProvider = new DatabaseConnectionStringProvider(baseConfiguration))
        {
            await new DatabaseMigrationRunner(
                migrationProvider, migrationCatalog, new DatabaseRuntimePrivilegeManager(), baseConfiguration,
                NullLogger<DatabaseMigrationRunner>.Instance)
                .ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju, ct);
        }
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Administrator, $"""
            update deployment_maintenance set release_id='{ReleaseId:D}',version=1,popup_version=1,state='Active',
                title='Synthetic provisioning',body='Synthetic provisioning',
                starts_at_utc=now()-interval '1 hour',expected_ends_at_utc=now()+interval '1 hour' where id=1
            """, ct);

        await AssertFailureAsync(G2MaterialCatalogProvisioner.TargetInvalidCode,
            Configuration(db, BusinessUnitCodes.Osan, ReleaseId, reviewSafe: false), migrationCatalog, ct);
        await AssertFailureAsync(G2MaterialCatalogProvisioner.ReviewSafeCode,
            Configuration(db, BusinessUnitCodes.Cheongju, ReleaseId, reviewSafe: true), migrationCatalog, ct);
        await AssertFailureAsync(G2MaterialCatalogProvisioner.MaintenanceStateMismatchCode,
            Configuration(db, BusinessUnitCodes.Cheongju, Guid.Parse("71000000-0000-0000-0000-000000000002"), reviewSafe: false),
            migrationCatalog, ct);
        Assert.Equal(0L, await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator, "select count(*) from g2_material_items", ct));

        var configuration = Configuration(db, BusinessUnitCodes.Cheongju, ReleaseId, reviewSafe: false);
        await using var provider = new DatabaseConnectionStringProvider(configuration);
        var provisioner = new G2MaterialCatalogProvisioner(configuration, provider, migrationCatalog);
        await using var blocker = await db.OpenAsync(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration, ct);
        await using var blockerCommand = blocker.CreateCommand();
        blockerCommand.CommandText = "select pg_advisory_lock_shared(9070125)";
        await blockerCommand.ExecuteNonQueryAsync(ct);
        var blockerHeld = true;
        Task<int>? pending = null;
        try
        {
            pending = provisioner.ProvisionAsync(ct);
            await WaitForAdvisoryLockAsync(db, 9070125, granted: false, ct);
            Assert.False(pending.IsCompleted);
            blockerCommand.CommandText = "select pg_advisory_unlock_shared(9070125)";
            await blockerCommand.ExecuteNonQueryAsync(ct);
            blockerHeld = false;
            Assert.Equal(92, await pending);
        }
        finally
        {
            if (blockerHeld)
            {
                blockerCommand.CommandText = "select pg_advisory_unlock_shared(9070125)";
                await blockerCommand.ExecuteNonQueryAsync(ct);
            }
            if (pending is not null) await pending;
        }

        Assert.Equal(92L, await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator, "select count(*) from g2_material_items", ct));
        Assert.Equal(0L, await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator, "select count(*) from g2_material_counts", ct));
        await AssertFailureAsync(G2MaterialCatalogProvisioner.CatalogNotEmptyCode,
            configuration, migrationCatalog, ct);
        Assert.False(await db.ReadScalarAsync<bool>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Administrator,
            "select to_regclass('public.g2_material_items') is not null", ct));
    }

    private static IConfiguration Configuration(BusinessUnitIsolationTests.IsolationDatabaseSet db,
        string target, Guid releaseId, bool reviewSafe)
    {
        var values = new Dictionary<string, string?>(db.ConfigurationValues, StringComparer.OrdinalIgnoreCase)
        {
            ["Database:MigrationTarget"] = target,
            ["G2Materials:ProvisionReleaseId"] = releaseId.ToString("D"),
            ["ReviewSafe:Enabled"] = reviewSafe.ToString()
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static string CatalogDigest(IEnumerable<G2MaterialCatalog> catalog)
    {
        var normalized = catalog.Select(item => new
        {
            item.Id,
            item.SourceRow,
            item.Category,
            item.Vendor,
            item.Name,
            item.Code,
            item.Spec,
            item.Unit,
            SetQuantity = item.SetQuantity.ToString("G29", CultureInfo.InvariantCulture),
            item.PackBox,
            item.PackPalletEa,
            item.PackPalletBox,
            item.PackNote,
            item.ImageKeys
        });
        return Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalized))));
    }

    private static async Task AssertFailureAsync(string expectedCode, IConfiguration configuration,
        DatabaseMigrationCatalog migrationCatalog, CancellationToken ct)
    {
        await using var provider = new DatabaseConnectionStringProvider(configuration);
        var provisioner = new G2MaterialCatalogProvisioner(configuration, provider, migrationCatalog);
        var error = await Assert.ThrowsAsync<G2MaterialCatalogProvisioningException>(
            () => provisioner.ProvisionAsync(ct));
        Assert.Equal(expectedCode, error.Code);
    }

    private static async Task WaitForAdvisoryLockAsync(
        BusinessUnitIsolationTests.IsolationDatabaseSet db, long key, bool granted, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await db.ReadScalarAsync<bool>(BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"select exists(select 1 from pg_locks where locktype='advisory' and database=(select oid from pg_database where datname=current_database()) and classid=0 and objid={key} and granted={granted.ToString().ToLowerInvariant()})",
                ct)) return;
            await Task.Delay(25, ct);
        }
        throw new TimeoutException("Synthetic G2 catalog provisioning did not reach its maintenance lock barrier.");
    }
}
