using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class BusinessSchemaMigrationCatalogTests
{
    [Fact]
    public void TargetCatalog_CombinesCommonHistoryWithOnlySelectedBusinessMigrations()
    {
        using var root = new OwnedTemporaryDirectory("emi-qms-business-catalog-");
        var common = Directory.CreateDirectory(Path.Combine(root.Path, "migrations"));
        var business = Directory.CreateDirectory(Path.Combine(root.Path, "business-migrations"));
        var cheongju = Directory.CreateDirectory(Path.Combine(business.FullName, "cheongju"));
        var osan = Directory.CreateDirectory(Path.Combine(business.FullName, "osan"));
        File.WriteAllText(Path.Combine(common.FullName, "0001_common_one.sql"), "select 1;");
        File.WriteAllText(Path.Combine(common.FullName, "0002_common_two.sql"), "select 2;");
        File.WriteAllText(Path.Combine(cheongju.FullName, "0003_cheongju_only.sql"), "select 3;");
        File.WriteAllText(Path.Combine(osan.FullName, "0003_osan_only.sql"), "select 3;");
        var catalog = DatabaseMigrationCatalog.FromPaths(common.FullName, business.FullName);

        var cheongjuSnapshot = catalog.GetSnapshot(BusinessUnitCodes.Cheongju);
        var osanSnapshot = catalog.GetSnapshot(BusinessUnitCodes.Osan);

        Assert.Equal(
            ["0001_common_one", "0002_common_two", "0003_cheongju_only"],
            cheongjuSnapshot.Versions);
        Assert.Equal(
            ["0001_common_one", "0002_common_two", "0003_osan_only"],
            osanSnapshot.Versions);
        Assert.Equal("0003_cheongju_only", cheongjuSnapshot.LatestVersion);
        Assert.Equal("0003_osan_only", osanSnapshot.LatestVersion);
    }

    [Fact]
    public void TargetCatalog_RejectsMissingBusinessMigrationInsteadOfAcceptingCommonHistory()
    {
        using var root = new OwnedTemporaryDirectory("emi-qms-business-catalog-empty-");
        var common = Directory.CreateDirectory(Path.Combine(root.Path, "migrations"));
        var business = Directory.CreateDirectory(Path.Combine(root.Path, "business-migrations"));
        Directory.CreateDirectory(Path.Combine(business.FullName, "cheongju"));
        Directory.CreateDirectory(Path.Combine(business.FullName, "osan"));
        File.WriteAllText(Path.Combine(common.FullName, "0001_common.sql"), "select 1;");
        var catalog = DatabaseMigrationCatalog.FromPaths(common.FullName, business.FullName);

        var exception = Assert.Throws<MigrationCatalogException>(
            () => catalog.GetSnapshot(BusinessUnitCodes.Cheongju));

        Assert.Equal("migration_catalog_business_target_empty", exception.Reason);
    }

    [Fact]
    public async Task MultiDatabaseRunner_RequiresExplicitTargetBeforeOpeningAnyConnection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BusinessUnits:Enabled"] = "true"
            })
            .Build();
        using var root = new OwnedTemporaryDirectory("emi-qms-business-runner-");
        File.WriteAllText(Path.Combine(root.Path, "0001_common.sql"), "select 1;");
        var runner = new DatabaseMigrationRunner(
            new DatabaseConnectionStringProvider(configuration),
            DatabaseMigrationCatalog.FromPath(root.Path),
            new DatabaseRuntimePrivilegeManager(),
            configuration,
            NullLogger<DatabaseMigrationRunner>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.ApplyAndVerifyAsync(TestContext.Current.CancellationToken));

        Assert.Equal("business_unit_migration_target_required", exception.Message);
    }

    private sealed class OwnedTemporaryDirectory : IDisposable
    {
        public OwnedTemporaryDirectory(string prefix)
        {
            Path = Directory.CreateTempSubdirectory(prefix).FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            var directory = new DirectoryInfo(Path);
            if (directory.Exists)
            {
                directory.Delete(recursive: true);
            }
        }
    }
}
