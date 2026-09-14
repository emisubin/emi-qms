using Emi.Qms.Api.InteriorBusbar;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;
namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarStoreTests
{
    public static bool HasDatabase => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUSBAR_TEST_CONNECTION_STRING"));
    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]

    public async Task ProductionSnapshotsBomAndWorker_SecondPhotoTime_AndReversalUsesOriginalConsumption()
    {
        await using var f = await Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        var material = await f.Store.Master("materials", new(null, "M", "Material", "개", "도급"), f.Actor);
        var worker = await f.Store.Master("workers", new(null, "W", "Original worker"), f.Actor);
        await f.Store.Bom(new(family, [new(material, 3)]), f.Actor);
        var product = await f.Store.Product(new(Guid.NewGuid(), family, worker), f.Actor);
        await f.Store.Photo(product, "front", [1], null, f.Actor);
        Assert.Equal("Draft", (await f.Store.GetProduct(product))["status"]);
        Assert.Equal(0m, await f.Balance("Finished", family));
        f.Clock.Now = f.Clock.Now.AddHours(1);
        await f.Store.Photo(product, "back", [2], null, f.Actor);
        var completed = await f.Store.GetProduct(product);
        Assert.Equal("Complete", completed["status"]);
        Assert.Equal(f.Clock.Now.UtcDateTime, completed["manufacturedAtUtc"]);
        Assert.Equal(1m, await f.Balance("Finished", family));
        Assert.Equal(-3m, await f.Balance("Material", material));
        await f.Store.Master("workers", new(worker, "W", "New worker name"), f.Actor);
        Assert.Equal("Original worker", (await f.Store.GetProduct(product))["workerName"]);
        await f.Store.Bom(new(family, [new(material, 7)]), f.Actor);
        await f.Store.Photo(product, "front", [3], "사진 수정", f.Actor);
        Assert.Equal(f.Clock.Now.UtcDateTime, (await f.Store.GetProduct(product))["manufacturedAtUtc"]);
        Assert.Equal(-3m, await f.Balance("Material", material));
        var cancel = new BusbarReverseRequest(Guid.NewGuid(), "오등록 취소");
        await f.Store.CancelProduct(product, cancel, f.Actor);
        await f.Store.CancelProduct(product, cancel, f.Actor);
        Assert.Equal(0m, await f.Balance("Finished", family));
        Assert.Equal(0m, await f.Balance("Material", material));
    }
    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]

    public async Task ConcurrentShipmentsCannotOversellAndRequestReplayCannotChangeQuantity()
    {
        await using var f = await Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        await f.Store.Settings(new("COMMON"), f.Actor);
        await f.Store.Adjustment(new(Guid.NewGuid(), "Finished", family, 30, "기초", true), f.Actor);
        var project = await f.Store.Project(new(null, "Project", "", family, 60, "Destination", new(2026, 10, 1)), f.Actor);
        var first = new BusbarShipmentRequest(Guid.NewGuid(), project, 20);
        var second = new BusbarShipmentRequest(Guid.NewGuid(), project, 20);
        async Task<bool> Ship(BusbarShipmentRequest r)
        {
            try
            {
                await f.Store.Shipment(r, f.Actor);
                return true;
            }
            catch (BusbarException)
            {
                return false;
            }
        }
        var results = await Task.WhenAll(Ship(first), Ship(second));
        Assert.Single(results, x => x);
        Assert.Equal(10m, await f.Balance("Finished", family));
        var success = results[0] ? first : second;
        await f.Store.Shipment(success, f.Actor);
        Assert.Equal(10m, await f.Balance("Finished", family));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Shipment(success with
        {
            Quantity = 1
        }
, f.Actor));
        var id = await f.Store.Shipment(success, f.Actor);
        await f.Store.Reverse(id, new(Guid.NewGuid(), "출하 취소"), f.Actor);
        Assert.Equal(30m, await f.Balance("Finished", family));
    }
    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]

    public async Task MissingBomRollsBackSecondPhotoAndPurchaseDoesNotAddInventory()
    {
        await using var f = await Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        var material = await f.Store.Master("materials", new(null, "M", "Material", "개", "사급"), f.Actor);
        var worker = await f.Store.Master("workers", new(null, "W", "Worker"), f.Actor);
        var product = await f.Store.Product(new(Guid.NewGuid(), family, worker), f.Actor);
        await f.Store.Photo(product, "front", [1], null, f.Actor);
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Photo(product, "back", [2], null, f.Actor));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.GetPhoto(product, "back"));
        await f.Store.Settings(new("COMMON"), f.Actor);
        var order = await f.Store.Purchase(new(null, "PO", material, 10, new(2026, 9, 9)), f.Actor);
        Assert.Equal(0m, await f.Balance("Material", material));
        var receipt = await f.Store.Receipt(new(Guid.NewGuid(), order, 4), f.Actor);
        Assert.Equal(4m, await f.Balance("Material", material));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Receipt(new(Guid.NewGuid(), order, 7), f.Actor));
        await f.Store.Reverse(receipt, new(Guid.NewGuid(), "입고 취소"), f.Actor);
        Assert.Equal(0m, await f.Balance("Material", material));
    }
    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]

    public async Task ExcelApplyIsAtomicAndOnlyIdCanUpdateDuplicateNames()
    {
        await using var f = await Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        await f.Store.Settings(new("COMMON"), f.Actor);
        var row = new BusbarProjectRequest(null, "Same name", "", family, 60, "Place", new(2026, 10, 1));
        await f.Store.ApplyProjects([row, row], f.Actor);
        Assert.Equal(2L, await f.Scalar("select count(*) from busbar_projects"));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.ApplyProjects([row, row with {
 RequestedQuantity = 0 }
], f.Actor));
        Assert.Equal(2L, await f.Scalar("select count(*) from busbar_projects"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]
    public async Task ThreePartialShipmentsCompleteAndReversalReopensProject_WithWorkspaceAggregates()
    {
        await using var f = await Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        await f.Store.Settings(new("COMMON"), f.Actor);
        await f.Store.Adjustment(new(Guid.NewGuid(), "Finished", family, 60, "기초", true), f.Actor);
        var plan = await f.Store.Plan(new(null, family, new(2026, 10, 1), 2), f.Actor);
        await f.Store.Plan(new(plan, family, new(2026, 10, 1), 7), f.Actor);
        Assert.Equal(2L, await f.Scalar("select (before_value->>'quantity')::bigint from busbar_audit where entity_kind='Plan' and before_value <> '{}'::jsonb"));
        var row = new BusbarProjectRequest(null, "P", "", family, 60, "Place", new(2026, 10, 1));
        var project = await f.Store.Project(row, f.Actor);
        await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        var last = await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        Assert.Equal(0m, await f.Balance("Finished", family));
        var workspace = (Dictionary<string, object?>)await f.Store.Workspace(true);
        var projects = (List<Dictionary<string, object?>>)workspace["projects"]!;
        Assert.Equal("Complete", projects.Single()["status"]);
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Project(row with { Id = project, RequestedQuantity = 59, Reason = "정정" }, f.Actor));
        await f.Store.Reverse(last, new(Guid.NewGuid(), "출하 취소"), f.Actor);
        workspace = (Dictionary<string, object?>)await f.Store.Workspace(true);
        projects = (List<Dictionary<string, object?>>)workspace["projects"]!;
        Assert.Equal("InProgress", projects.Single()["status"]);
        Assert.Equal(20, projects.Single()["remainingQuantity"]);
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]
    public async Task PlanPreparesEmptyProducts_ReplayAndGrowthDoNotDuplicate_WorkerAndNumberWaitForPhotos()
    {
        await using var f = await Fixture.Create();
        var family = await f.Store.Master("product-families", new(null,"F","Family"), f.Actor);
        var material = await f.Store.Master("materials", new(null,"M","Material","개","도급"), f.Actor);
        var worker = await f.Store.Master("workers", new(null,"W","Worker"), f.Actor);
        await f.Store.Bom(new(family,[new(material,3)]), f.Actor);
        var request = new BusbarPlanRequest(Guid.NewGuid(), family, new(2026,10,1), 2);
        await Task.WhenAll(f.Store.Plan(request,f.Actor), f.Store.Plan(request,f.Actor));
        Assert.Equal(2L, await f.Scalar("select count(*) from busbar_products"));
        var workspace = (Dictionary<string,object?>)await f.Store.Workspace(true,planId:request.Id);
        var products = (List<Dictionary<string,object?>>)workspace["products"]!;
        Assert.All(products,p => { Assert.Null(p["workerId"]); Assert.Null(p["workerName"]); Assert.Null(p["number"]); Assert.Null(p["manufacturedAtUtc"]); Assert.Equal("Draft",p["status"]); });
        Assert.Equal(new[] {1,2}, products.Select(p => (int)p["planSequence"]!));
        var first = (Guid)products[0]["id"]!;
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Photo(first,"front",[1],null,f.Actor));
        await f.Store.Photo(first,"front",[1],null,f.Actor,worker);
        Assert.Null((await f.Store.GetProduct(first))["number"]);
        Assert.Equal(0m,await f.Balance("Finished",family));
        await f.Store.Photo(first,"back",[2],null,f.Actor);
        Assert.NotNull((await f.Store.GetProduct(first))["number"]);
        Assert.Equal(1m,await f.Balance("Finished",family));
        Assert.Equal(-3m,await f.Balance("Material",material));
        await f.Store.Plan(request with {Quantity=4},f.Actor);
        await f.Store.Plan(request with {Quantity=4},f.Actor);
        Assert.Equal(4L,await f.Scalar("select count(*) from busbar_products"));
        workspace = (Dictionary<string,object?>)await f.Store.Workspace(true,planId:request.Id);
        var plans = (List<Dictionary<string,object?>>)workspace["plans"]!;
        Assert.Equal(1L,plans.Single()["actualQuantity"]);
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]
    public async Task PlanReductionWithdrawsOnlyUntouchedDrafts_AndRejectsChangingDateOrFamily()
    {
        await using var f = await Fixture.Create();
        var family = await f.Store.Master("product-families", new(null,"F","Family"), f.Actor);
        var otherFamily = await f.Store.Master("product-families", new(null,"G","Other"), f.Actor);
        var worker = await f.Store.Master("workers", new(null,"W","Worker"), f.Actor);
        var request = new BusbarPlanRequest(Guid.NewGuid(),family,new(2026,10,1),3);
        await f.Store.Plan(request,f.Actor);
        var workspace = (Dictionary<string,object?>)await f.Store.Workspace(true,planId:request.Id);
        var products = (List<Dictionary<string,object?>>)workspace["products"]!;
        await f.Store.CorrectProduct((Guid)products[0]["id"]!,new(worker,"작업자 지정"),f.Actor);
        await f.Store.Photo((Guid)products[1]["id"]!,"front",[1],null,f.Actor,worker);
        await f.Store.Plan(request with {Quantity=2},f.Actor);
        Assert.Equal("Cancelled",(await f.Store.GetProduct((Guid)products[2]["id"]!))["status"]);
        await f.Store.Plan(request with {Quantity=2},f.Actor);
        Assert.Equal(3L,await f.Scalar("select count(*) from busbar_products"));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Plan(request with {Quantity=1},f.Actor));
        Assert.Equal(2L,await f.Scalar("select quantity::bigint from busbar_plans"));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Plan(request with {PlanDate=new(2026,10,2)},f.Actor));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Plan(request with {ProductFamilyId=otherFamily},f.Actor));
        await f.Store.Plan(request with {Quantity=3},f.Actor);
        Assert.Equal(4L,await f.Scalar("select max(plan_sequence)::bigint from busbar_products"));
        Assert.Equal(1L,await f.Scalar("select count(*) from busbar_audit where reason='생산계획 수량 감소로 미착수 제품 철회'"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]
    public async Task PlanFilterPaginatesOnlyLinkedProducts_AndInitializesLegacyPlanOnce()
    {
        await using var f = await Fixture.Create();
        var family = await f.Store.Master("product-families", new(null,"F","Family"), f.Actor);
        var worker = await f.Store.Master("workers",new(null,"W","Worker"),f.Actor);
        var legacyProduct = await f.Store.Product(new(Guid.NewGuid(),family,worker),f.Actor);
        var legacyPlan = Guid.NewGuid();
        await using(var c = new NpgsqlConnection(f.Connection))
        {
            await c.OpenAsync(TestContext.Current.CancellationToken);
            await using var cmd = new NpgsqlCommand("insert into busbar_plans(id,product_family_id,plan_date,quantity) values(@id,@family,'2026-10-01',3)",c);
            cmd.Parameters.AddWithValue("id",legacyPlan);cmd.Parameters.AddWithValue("family",family);
            await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        var request = new BusbarPlanRequest(legacyPlan,family,new(2026,10,1),3);
        await f.Store.Plan(request,f.Actor);
        await f.Store.Plan(request,f.Actor);
        await f.Store.Plan(new(Guid.NewGuid(),family,new(2026,10,2),5),f.Actor);
        var workspace = (Dictionary<string,object?>)await f.Store.Workspace(true,2,2,legacyPlan);
        var products = (List<Dictionary<string,object?>>)workspace["products"]!;
        Assert.Single(products);Assert.Equal(3,products[0]["planSequence"]);
        Assert.Null((await f.Store.GetProduct(legacyProduct))["planId"]);
        Assert.Equal(9L,await f.Scalar("select count(*) from busbar_products"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task FamilyDateAndStatusFiltersApplyBeforePaginationAndCount()
    {
        await using var f = await Fixture.Create();
        var family = await f.Store.Master("product-families", new(null,"A","Family A"),f.Actor);
        var other = await f.Store.Master("product-families", new(null,"B","Family B"),f.Actor);
        var worker = await f.Store.Master("workers",new(null,"W","Worker"),f.Actor);
        var material = await f.Store.Master("materials",new(null,"M","Material","m","도급"),f.Actor);
        await f.Store.Bom(new(family,[new(material,1)]),f.Actor);
        var plan = await f.Store.Plan(new(Guid.NewGuid(),family,new(2026,9,10),4),f.Actor);
        await f.Store.Plan(new(Guid.NewGuid(),family,new(2026,9,11),2),f.Actor);
        await f.Store.Plan(new(Guid.NewGuid(),other,new(2026,9,10),7),f.Actor);
        await f.Store.Product(new(Guid.NewGuid(),family,worker),f.Actor);
        var first = (Dictionary<string,object?>)await f.Store.Workspace(true,1,100,plan);
        var firstProducts = (List<Dictionary<string,object?>>)first["products"]!;
        var completed = (Guid)firstProducts[0]["id"]!;
        await f.Store.Photo(completed,"front",[1],null,f.Actor,worker);
        await f.Store.Photo(completed,"back",[2],null,f.Actor,worker);
        await f.Store.CancelProduct((Guid)firstProducts[1]["id"]!,new(Guid.NewGuid(),"test cancellation"),f.Actor);
        var filtered = (Dictionary<string,object?>)await f.Store.Workspace(true,2,1,null,family,new(2026,9,10),new(2026,9,10),"Draft");
        var rows = (List<Dictionary<string,object?>>)filtered["products"]!;
        Assert.Single(rows);
        Assert.Equal(family,rows[0]["productFamilyId"]);
        Assert.Equal(plan,rows[0]["planId"]);
        Assert.Equal("Draft",rows[0]["status"]);
        Assert.Equal(4,rows[0]["planSequence"]);
        var firstPage = (Dictionary<string,object?>)await f.Store.Workspace(true,1,1,null,family,new(2026,9,10),new(2026,9,10),"Draft");
        Assert.Equal(3,Assert.Single((List<Dictionary<string,object?>>)firstPage["products"]!)["planSequence"]);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(filtered["pagination"]);
        Assert.Equal(2,json.GetProperty("productCount").GetInt32());
        var complete = (Dictionary<string,object?>)await f.Store.Workspace(true,1,100,null,family,new(2026,9,10),new(2026,9,10),"Complete");
        Assert.Equal(completed,Assert.Single((List<Dictionary<string,object?>>)complete["products"]!)["id"]);
        var inclusive = (Dictionary<string,object?>)await f.Store.Workspace(true,1,100,null,family,new(2026,9,10),new(2026,9,11),"Draft");
        Assert.Equal(4,((List<Dictionary<string,object?>>)inclusive["products"]!).Count);
        var noDate = (Dictionary<string,object?>)await f.Store.Workspace(true,1,100,null,family,status:"Draft");
        Assert.Equal(5,((List<Dictionary<string,object?>>)noDate["products"]!).Count);
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.Workspace(true,productFamilyId:family,planDateFrom:new(2026,9,11),planDateTo:new(2026,9,10)));
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.Workspace(true,status:"not-a-status"));
    }

    internal sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 9, 9, 1, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public required string Connection
        {
            get;
            init;
        }
        public required string BaseConnection
        {
            get;
            init;
        }
        public required string Schema
        {
            get;
            init;
        }
        public required InteriorBusbarStore Store
        {
            get;
            init;
        }
        public required Clock Clock
        {
            get;
            init;
        }
        public Guid Actor
        {
            get;
            init;
        }
 = Guid.NewGuid();
        public static async Task<Fixture> Create(InteriorBusbarPublicationOptions? publicationOptions = null, bool applyCommercialMigration = true)
        {
            var baseConnection = Environment.GetEnvironmentVariable("BUSBAR_TEST_CONNECTION_STRING") ?? throw new InvalidOperationException("Set BUSBAR_TEST_CONNECTION_STRING to an explicitly disposable synthetic database.");
            var builder = new NpgsqlConnectionStringBuilder(baseConnection);
            if (builder.Database != "busbar_test" || builder.Host != "127.0.0.1" || builder.Port != 55490) throw new InvalidOperationException("Busbar tests require the dedicated local busbar_test database on port 55490.");
            var schema = "busbar_" + Guid.NewGuid().ToString("N");
            await using (var c = new NpgsqlConnection(baseConnection))
            {
                await c.OpenAsync(TestContext.Current.CancellationToken);
                await new NpgsqlCommand($"create schema {schema}", c).ExecuteNonQueryAsync();
            }
            builder.SearchPath = schema;
            var clock = new Clock();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:QmsDatabase"] = builder.ConnectionString
            }
           ).Build();
            var f = new Fixture
            {
                Connection = builder.ConnectionString,
                BaseConnection = baseConnection,
                Schema = schema,
                Clock = clock,
                Store = new(new(config), clock, publicationOptions)
            }
;
            await using (var c = new NpgsqlConnection(f.Connection))
            {
                await c.OpenAsync(TestContext.Current.CancellationToken);
                await new NpgsqlCommand("create table roles(id uuid primary key,code text unique,name text);create table qms_users(id uuid primary key,display_name text default 'Synthetic manager');", c).ExecuteNonQueryAsync();
                var root = AppContext.BaseDirectory;
                while (!Directory.Exists(Path.Combine(root, "database", "migrations"))) root = Directory.GetParent(root)!.FullName;
                await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0090_interior_busbar.sql")), c).ExecuteNonQueryAsync();
                await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0091_interior_busbar_planned_products.sql")), c).ExecuteNonQueryAsync();
                await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0092_interior_busbar_product_qr.sql")), c).ExecuteNonQueryAsync();
                if (applyCommercialMigration) await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0093_interior_busbar_commercial_data.sql")), c).ExecuteNonQueryAsync();
                await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0094_interior_busbar_ecount_queue.sql")), c).ExecuteNonQueryAsync();
                await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0095_interior_busbar_ecount_runtime.sql")), c).ExecuteNonQueryAsync();
                await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0096_interior_busbar_project_creator.sql")), c).ExecuteNonQueryAsync();
                await using var cmd = new NpgsqlCommand("insert into qms_users(id) values(@id)", c);
                cmd.Parameters.AddWithValue("id", f.Actor);
                await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            await f.Store.EcountEmployee(new(f.Actor, "SYN-EMP", "Synthetic setup"), f.Actor);
            return f;
        }
        public async Task<decimal> Balance(string kind, Guid item)
        {
            await using var c = new NpgsqlConnection(Connection);
            await c.OpenAsync(TestContext.Current.CancellationToken);
            await using var cmd = new NpgsqlCommand("select coalesce((select balance from busbar_stock where stock_kind=@kind and item_id=@id),0)", c);
            cmd.Parameters.AddWithValue("kind", kind);
            cmd.Parameters.AddWithValue("id", item);
            return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
        }
        public async Task<long> Scalar(string sql)
        {
            await using var c = new NpgsqlConnection(Connection);
            await c.OpenAsync(TestContext.Current.CancellationToken);
            return Convert.ToInt64(await new NpgsqlCommand(sql, c).ExecuteScalarAsync());
        }
        public async ValueTask DisposeAsync()
        {
            await using var c = new NpgsqlConnection(BaseConnection);
            await c.OpenAsync(TestContext.Current.CancellationToken);
            await new NpgsqlCommand($"drop schema {Schema} cascade", c).ExecuteNonQueryAsync();
        }
    }
}
