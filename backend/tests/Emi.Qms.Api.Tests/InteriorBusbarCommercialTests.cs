using System.Text.Json;
using Emi.Qms.Api.InteriorBusbar;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarCommercialTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task AllProjectsUseCurrentFamilyPriceWithoutChangingInventory()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        await f.Store.Settings(new("SYN-PROJECT", "SYN-CUSTOMER", "SYNWH"), f.Actor);
        var family = await f.Store.Master("product-families", new(null, "F", "Synthetic",
            EcountProductCode: "SYN-PRODUCT", StandardUnitPrice: 12500), f.Actor);
        var request = new BusbarProjectRequest(null, "Synthetic project", "SYN-WO", family, 60, "Synthetic destination", new(2026, 10, 1));
        var id = await f.Store.Project(request, f.Actor);
        await f.Store.Project(request with { Id = id, Reason = "Change delivery only", DueDate = new(2026, 10, 2) }, f.Actor);
        var preview = JsonSerializer.SerializeToElement(await f.Store.CommercialPreview(id));
        Assert.Equal(12500, preview.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(750000, preview.GetProperty("supplyAmount").GetDecimal());
        Assert.Equal(75000, preview.GetProperty("vatAmount").GetDecimal());
        Assert.Equal(825000, preview.GetProperty("totalAmount").GetDecimal());
        Assert.Equal("SYN-WO", preview.GetProperty("workOrderNumber").GetString());
        Assert.Equal("", preview.GetProperty("purchaseOrderNumber").GetString());
        Assert.Empty(preview.GetProperty("missingFields").EnumerateArray());
        Assert.False(preview.GetProperty("transmissionEnabled").GetBoolean());
        await f.Store.Master("product-families", new(family, "F", "Synthetic", EcountProductCode: "SYN-PRODUCT", StandardUnitPrice: 12000), f.Actor);
        preview = JsonSerializer.SerializeToElement(await f.Store.CommercialPreview(id));
        Assert.Equal(792000, preview.GetProperty("totalAmount").GetDecimal());
        var second = await f.Store.Project(request with { Name = "Second project" }, f.Actor);
        Assert.Equal(12000m, JsonSerializer.SerializeToElement(await f.Store.CommercialPreview(second)).GetProperty("unitPrice").GetDecimal());
        Assert.Equal(0, await f.Scalar("select count(*) from busbar_operations"));
        Assert.Equal(0m, await f.Balance("Finished", family));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task UnsetFamilyPriceIsUnknownAndZeroPriceIsDistinct()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        await f.Store.Settings(new("SYN-PROJECT"), f.Actor);
        var family = await f.Store.Master("product-families", new(null, "F", "Synthetic"), f.Actor);
        var request = new BusbarProjectRequest(null, "Synthetic project", "", family, 1, "Synthetic", new(2026, 10, 1));
        var id = await f.Store.Project(request, f.Actor);
        await f.Store.Project(request with { Id = id, Reason = "Preserve unknown price" }, f.Actor);
        var preview = JsonSerializer.SerializeToElement(await f.Store.CommercialPreview(id));
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("unitPrice").ValueKind);
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("totalAmount").ValueKind);
        Assert.Contains("제품군 기준 단가", preview.GetProperty("missingFields").EnumerateArray().Select(x => x.GetString()));
        await f.Store.Master("product-families", new(family, "F", "Synthetic", StandardUnitPrice: 0), f.Actor);
        preview = JsonSerializer.SerializeToElement(await f.Store.CommercialPreview(id));
        Assert.Equal(0, preview.GetProperty("totalAmount").GetDecimal());
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task InvalidFamilyPricesFailAndSettingsPreserveOmittedCodes()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        await f.Store.Settings(new("SYN-P", "SYN-C", "SYNWH"), f.Actor);
        await f.Store.Settings(new("SYN-P2"), f.Actor);
        var workspace = JsonSerializer.SerializeToElement(await f.Store.Workspace(true));
        Assert.Equal("SYN-C", workspace.GetProperty("settings").GetProperty("ecountCustomerCode").GetString());
        Assert.Equal("SYNWH", workspace.GetProperty("settings").GetProperty("ecountWarehouseCode").GetString());
        var family = await f.Store.Master("product-families", new(null, "F", "Synthetic"), f.Actor);
        var request = new BusbarProjectRequest(null, "Synthetic", "", family, 1, "Synthetic", new(2026, 10, 1));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Master("product-families", new(family, "F", "Synthetic", StandardUnitPrice: -1), f.Actor));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Master("product-families", new(null, "BAD", "Synthetic", StandardUnitPrice: 0.00001m), f.Actor));
        Assert.Equal(0, await f.Scalar("select count(*) from busbar_projects"));
        var id = await f.Store.Project(request, f.Actor);
        var preview = JsonSerializer.SerializeToElement(await f.Store.CommercialPreview(id));
        Assert.Equal("SYN-C", preview.GetProperty("customerCode").GetString());
        Assert.Equal("SYNWH", preview.GetProperty("warehouseCode").GetString());
    }
    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task ImportedProjectsUseUpdatedFamilyPrice()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        await f.Store.Settings(new("SYN-P"), f.Actor);
        var family = await f.Store.Master("product-families", new(null, "F", "Synthetic", StandardUnitPrice: 12500), f.Actor);
        var request = new BusbarProjectRequest(null, "Imported", "SYN-WO", family, 2, "Synthetic", new(2026, 10, 1));
        await f.Store.ApplyProjects([request], f.Actor);
        var workspace = JsonSerializer.SerializeToElement(await f.Store.Workspace(true));
        var id = workspace.GetProperty("projects")[0].GetProperty("id").GetGuid();
        await f.Store.Master("product-families", new(family, "F", "Synthetic", StandardUnitPrice: 20000), f.Actor);
        await f.Store.ApplyProjects([request with { Id = id, Reason = "Legacy sheet correction" }], f.Actor);
        var preview = JsonSerializer.SerializeToElement(await f.Store.CommercialPreview(id));
        Assert.Equal(20000m, preview.GetProperty("unitPrice").GetDecimal());
        await f.Store.Master("product-families", new(family, "F", "Synthetic", StandardUnitPrice: 0.0001m), f.Actor);
        preview = JsonSerializer.SerializeToElement(await f.Store.CommercialPreview(id));
        Assert.Equal(0.00002m, preview.GetProperty("vatAmount").GetDecimal());
    }
    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task CommercialMigrationPreservesHistoricalRowsWithoutInventingPrices()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create(applyCommercialMigration: false);
        await using var connection = new Npgsql.NpgsqlConnection(f.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        const string seed = """
            insert into busbar_product_families values('10000000-0000-0000-0000-000000000001','SYN-F','Historical',true);
            insert into busbar_projects values('10000000-0000-0000-0000-000000000002','Historical','SYN-WO','SYN-P','10000000-0000-0000-0000-000000000001',60,'Synthetic','2026-10-01');
            insert into busbar_stock values('Finished','10000000-0000-0000-0000-000000000001',30);
            """;
        await new Npgsql.NpgsqlCommand(seed, connection).ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "database", "migrations"))) root = Directory.GetParent(root)!.FullName;
        await new Npgsql.NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0093_interior_busbar_commercial_data.sql"), TestContext.Current.CancellationToken), connection).ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, await f.Scalar("select count(*) from busbar_projects where requested_quantity=60 and customer_job_number='SYN-WO'"));
        Assert.Equal(1, await f.Scalar("select count(*) from busbar_product_families where standard_unit_price is null and ecount_product_code is null"));
        Assert.Equal(0, await f.Scalar("select count(*) from information_schema.columns where table_schema=current_schema() and table_name='busbar_projects' and column_name='unit_price'"));
        Assert.Equal(30m, await f.Balance("Finished", Guid.Parse("10000000-0000-0000-0000-000000000001")));
    }
}
