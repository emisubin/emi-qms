using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.InteriorBusbar;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Theory]
    [InlineData("missing", "ecount")]
    [InlineData("missing", "publication")]
    [InlineData("unexpected", "ecount")]
    [InlineData("unexpected", "publication")]
    public async Task BusinessSchemaWorkerBoundary_RejectsInexactCheongjuLedgerBeforeProvidersOrMutations(
        string mismatch,
        string worker)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var fixture = await PrepareWorkerBoundaryFixtureAsync(databases, ct);

        if (mismatch == "missing")
        {
            await databases.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Migration,
                "delete from schema_migrations where version='0131_cheongju_business_schema'", ct);
        }
        else
        {
            await databases.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Migration,
                "insert into schema_migrations(version) values('9999_unexpected_worker_boundary')", ct);
        }

        var cheongjuBefore = await SnapshotOwnedDataAsync(databases, BusinessUnitCodes.Cheongju, ct);
        var osanBefore = await SnapshotOwnedDataAsync(databases, BusinessUnitCodes.Osan, ct);
        var ecountClient = new WorkerBoundaryEcountClient();
        var publicationSink = new WorkerBoundaryPublicationSink();

        BusinessUnitContextUnavailableException error;
        if (worker == "ecount")
        {
            using var ecount = new InteriorBusbarEcountWorker(
                fixture.Cheongju, fixture.Validator, fixture.EcountOptions, ecountClient, TimeProvider.System,
                NullLogger<InteriorBusbarEcountWorker>.Instance);
            error = await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(
                () => ecount.RunOnceAsync(ct));
        }
        else
        {
            using var publication = new InteriorBusbarPublicationWorker(
                fixture.Cheongju, fixture.Validator, fixture.PublicationOptions, publicationSink,
                NullLogger<InteriorBusbarPublicationWorker>.Instance);
            error = await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(
                () => publication.PublishNextAsync(ct));
        }

        Assert.Equal("business_unit_database_ledger_mismatch", error.Reason);
        Assert.Equal(0, ecountClient.Authentications);
        Assert.Equal(0, ecountClient.Sends);
        Assert.Equal(0, publicationSink.Publishes);
        AssertWorkerBoundarySnapshotEqual(cheongjuBefore,
            await SnapshotOwnedDataAsync(databases, BusinessUnitCodes.Cheongju, ct));
        AssertWorkerBoundarySnapshotEqual(osanBefore,
            await SnapshotOwnedDataAsync(databases, BusinessUnitCodes.Osan, ct));
    }

    [Fact]
    public async Task BusinessSchemaWorkerBoundary_ExactLedgerProcessesCheongjuAndLeavesOsanUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var fixture = await PrepareWorkerBoundaryFixtureAsync(databases, ct);
        var osanBefore = await SnapshotOwnedDataAsync(databases, BusinessUnitCodes.Osan, ct);
        var ecountClient = new WorkerBoundaryEcountClient();
        var publicationSink = new WorkerBoundaryPublicationSink();

        using var ecount = new InteriorBusbarEcountWorker(
            fixture.Cheongju, fixture.Validator, fixture.EcountOptions, ecountClient, TimeProvider.System,
            NullLogger<InteriorBusbarEcountWorker>.Instance);
        using var publication = new InteriorBusbarPublicationWorker(
            fixture.Cheongju, fixture.Validator, fixture.PublicationOptions, publicationSink,
            NullLogger<InteriorBusbarPublicationWorker>.Instance);

        Assert.True(await ecount.RunOnceAsync(ct));
        Assert.True(await publication.PublishNextAsync(ct));

        Assert.Equal(1, ecountClient.Authentications);
        Assert.Equal(1, ecountClient.Sends);
        Assert.Equal(1, publicationSink.Publishes);
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from busbar_ecount_jobs where state='Succeeded'", ct));
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from busbar_products where id='{fixture.ProductId:D}' and publication_state='Published'",
            ct));
        AssertWorkerBoundarySnapshotEqual(osanBefore,
            await SnapshotOwnedDataAsync(databases, BusinessUnitCodes.Osan, ct));
    }

    private static async Task<WorkerBoundaryFixture> PrepareWorkerBoundaryFixtureAsync(
        IsolationDatabaseSet databases,
        CancellationToken ct)
    {
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(databases.RepositoryRoot));
        var privileges = new DatabaseRuntimePrivilegeManager();
        await BootstrapTargetsAsync(new DatabaseRoleBootstrapper(databases.Configuration, privileges,
            NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        await MigrateTargetsAsync(new DatabaseMigrationRunner(provider, catalog, privileges,
            databases.Configuration, NullLogger<DatabaseMigrationRunner>.Instance), ct);

        var actor = Guid.NewGuid();
        await databases.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Migration, $"""
            insert into qms_users(id,development_user_key,display_name,is_active)
            values('{actor:D}','worker-boundary-actor','Synthetic Boundary Actor',true);
            """, ct);

        var ecountOptions = WorkerBoundaryEcountOptions();
        var publicationOptions = new InteriorBusbarPublicationOptions(
            true,
            new Uri("https://products.z1.web.core.windows.net/"),
            new Uri("https://products.blob.core.windows.net/"),
            "synthetic",
            PmsBaseUrl: new Uri("https://pms.example.test/"));
        var cheongju = new CheongjuDatabase(provider);
        var validator = new BusinessUnitDatabaseBoundaryValidator(provider,
            new MigrationLedgerInspector(catalog), new BusinessUnitDirectoryMigrationCatalog(catalog));
        var store = new InteriorBusbarStore(cheongju, TimeProvider.System,
            publicationOptions, ecountOptions);
        await store.Settings(new BusbarSettingsRequest("SYN-P", "SYN-C", "SYNWH"), actor);
        var family = await store.Master("product-families",
            new BusbarMasterRequest(null, "SYN-F", "Synthetic Family",
                EcountProductCode: "SYN-F", StandardUnitPrice: 12345m), actor);
        var worker = await store.Master("workers",
            new BusbarMasterRequest(null, "SYN-W", "Synthetic Worker"), actor);
        await store.Project(new BusbarProjectRequest(null, "Synthetic Project", "SYN-WO", family, 1,
            "Synthetic Destination", new DateOnly(2026, 10, 1)), actor);
        var product = await store.Product(new BusbarProductRequest(Guid.NewGuid(), family, worker), actor);

        return new WorkerBoundaryFixture(cheongju, validator, ecountOptions, publicationOptions, product);
    }

    private static InteriorBusbarEcountOptions WorkerBoundaryEcountOptions() =>
        InteriorBusbarEcountOptions.Load(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["InteriorBusbar:Ecount:Enabled"] = "true",
                ["InteriorBusbar:Ecount:Environment"] = "Test",
                ["InteriorBusbar:Ecount:CompanyCode"] = "SYN001",
                ["InteriorBusbar:Ecount:UserId"] = "Synthetic",
                ["InteriorBusbar:Ecount:ApiKey"] = "synthetic-not-a-real-key",
                ["InteriorBusbar:Ecount:SessionIdleMinutes"] = "30"
            }).Build());

    internal static BusinessUnitDatabaseBoundaryValidator CreateWorkerBoundaryValidator(
        IConfiguration configuration)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !Directory.Exists(Path.Combine(directory.FullName, "database", "migrations")))
        {
            directory = directory.Parent;
        }
        var repositoryRoot = directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find repository root for worker boundary tests.");
        var catalog = DatabaseMigrationCatalog.FromPaths(
            Path.Combine(repositoryRoot, "database", "migrations"),
            Path.Combine(repositoryRoot, "database", "business-migrations"));
        var provider = new DatabaseConnectionStringProvider(configuration);
        return new BusinessUnitDatabaseBoundaryValidator(provider,
            new MigrationLedgerInspector(catalog), new BusinessUnitDirectoryMigrationCatalog(catalog));
    }

    private static void AssertWorkerBoundarySnapshotEqual(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (table, rows) in expected)
            Assert.Equal(rows, actual[table]);
    }

    private sealed record WorkerBoundaryFixture(
        CheongjuDatabase Cheongju,
        BusinessUnitDatabaseBoundaryValidator Validator,
        InteriorBusbarEcountOptions EcountOptions,
        InteriorBusbarPublicationOptions PublicationOptions,
        Guid ProductId);

    private sealed class WorkerBoundaryEcountClient : IInteriorBusbarEcountClient
    {
        private bool hasSession;

        public bool HasSession => hasSession;
        public int Authentications { get; private set; }
        public int Sends { get; private set; }

        public Task<bool> AuthenticateAsync(CancellationToken cancellationToken)
        {
            Authentications++;
            hasSession = true;
            return Task.FromResult(true);
        }

        public Task<BusbarEcountResult> SendAsync(
            BusbarEcountAttempt attempt,
            CancellationToken cancellationToken)
        {
            Sends++;
            return Task.FromResult(new BusbarEcountResult("Succeeded", "SYN-SLIP"));
        }
    }

    private sealed class WorkerBoundaryPublicationSink : IInteriorBusbarPublicationSink
    {
        public int Publishes { get; private set; }

        public Task PublishAsync(string token, byte[] html, CancellationToken cancellationToken)
        {
            Publishes++;
            return Task.CompletedTask;
        }
    }
}
