using System.Text.Json;
using Emi.Qms.Api.InteriorBusbar;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarDeletionTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task MigrationAddsDeletionStateWithoutChangingExistingBusinessRows()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create(applyDeletionMigration: false);
        var family = Guid.NewGuid();
        var material = Guid.NewGuid();
        var worker = Guid.NewGuid();
        await using (var c = new NpgsqlConnection(f.Connection))
        {
            await c.OpenAsync(TestContext.Current.CancellationToken);
            await using (var seed = new NpgsqlCommand("""
                insert into busbar_product_families(id,code,name) values(@family,'F','Family');
                insert into busbar_materials(id,code,name,unit,supply_type) values(@material,'M','Material','개','도급');
                insert into busbar_workers(id,code,name) values(@worker,'W','Worker');
                insert into busbar_boms(id,product_family_id,version) values(gen_random_uuid(),@family,1);
                insert into busbar_projects(id,name,customer_job_number,common_project_code,product_family_id,requested_quantity,destination,due_date) values(gen_random_uuid(),'Project','WO','PJT',@family,1,'Destination','2026-10-01');
                insert into busbar_plans(id,product_family_id,plan_date,quantity) values(gen_random_uuid(),@family,'2026-10-01',0);
                insert into busbar_purchases(id,order_number,material_id,quantity,order_date,common_project_code) values(gen_random_uuid(),'PO',@material,1,'2026-10-01','PJT');
                """, c))
            {
                seed.Parameters.AddWithValue("family", family);
                seed.Parameters.AddWithValue("material", material);
                seed.Parameters.AddWithValue("worker", worker);
                await seed.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            var root = AppContext.BaseDirectory;
            while (!Directory.Exists(Path.Combine(root, "database", "migrations"))) root = Directory.GetParent(root)!.FullName;
            await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0117_interior_busbar_soft_delete.sql"), TestContext.Current.CancellationToken), c)
                .ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var workspace = JsonSerializer.SerializeToElement(await f.Store.Workspace(true));
        foreach (var collection in new[] { "productFamilies", "materials", "workers", "boms", "projects", "plans", "purchases" })
            Assert.False(Assert.Single(workspace.GetProperty(collection).EnumerateArray()).GetProperty("isDeleted").GetBoolean());
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task ProjectDeleteHoldsUnsentErp_PreservesSuccessfulSlip_AndRestoreRequiresManualRetry()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family", EcountProductCode: "ERP-F", StandardUnitPrice: 100), f.Actor);
        await f.Store.Settings(new("PJT", "CUST", "WH"), f.Actor);
        var pending = await f.Store.Project(new(null, "Pending", "WO-1", family, 2, "Destination", new(2026, 10, 1)), f.Actor);
        var pendingJob = Job(await f.Store.EcountStatus(pending)).GetProperty("id").GetGuid();
        await f.Store.Adjustment(new(Guid.NewGuid(), "Finished", family, 2, "Synthetic opening", true), f.Actor);
        var shipped = await f.ShipmentRequest(pending, 1);
        var shipmentId = await f.Store.Shipment(shipped, f.Actor);
        var shipment = await f.ShipmentRequest(pending, 1);

        await f.Store.SetDeleted("projects", pending, "잘못 등록", f.Actor, true);
        await f.Store.SetDeleted("projects", pending, "반복 삭제", f.Actor, true);
        Assert.Equal("Held", Job(await f.Store.EcountStatus(pending)).GetProperty("state").GetString());
        Assert.Null(await f.Store.ClaimEcountJob(pendingJob));
        var workspace = JsonSerializer.SerializeToElement(await f.Store.Workspace(true));
        Assert.True(workspace.GetProperty("projects")[0].GetProperty("isDeleted").GetBoolean());
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Project(new(pending, "Edited", "WO-1", family, 2, "Destination", new(2026, 10, 1), "수정"), f.Actor));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Shipment(shipment, f.Actor));
        Assert.Equal(1L, await f.Scalar($"select count(*) from busbar_shipments where id='{shipmentId}'"));

        await f.Store.SetDeleted("projects", pending, "재개", f.Actor, false);
        Assert.Equal("Held", Job(await f.Store.EcountStatus(pending)).GetProperty("state").GetString());
        await f.Store.RetryEcount(pendingJob, "관리자 재시도", f.Actor);
        Assert.Equal("Pending", Job(await f.Store.EcountStatus(pending)).GetProperty("state").GetString());

        var succeeded = await f.Store.Project(new(null, "Succeeded", "WO-2", family, 1, "Destination", new(2026, 10, 1)), f.Actor);
        var succeededJob = Job(await f.Store.EcountStatus(succeeded)).GetProperty("id").GetGuid();
        var attempt = Assert.IsType<BusbarEcountAttempt>(await f.Store.ClaimEcountJob(succeededJob));
        await f.Store.FinishEcountAttempt(attempt.Id, new("Succeeded", "2026/10/01 -1"));
        await f.Store.SetDeleted("projects", succeeded, "완료 건 정리", f.Actor, true);
        var preserved = Job(await f.Store.EcountStatus(succeeded));
        Assert.Equal("Succeeded", preserved.GetProperty("state").GetString());
        Assert.Equal("2026/10/01 -1", preserved.GetProperty("slipNumber").GetString());
        Assert.Equal(2L, await f.Scalar("select count(*) from busbar_projects"));
        Assert.Equal(3L, await f.Scalar("select count(*) from busbar_audit where entity_kind='Project' and reason in ('잘못 등록','재개','완료 건 정리')"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task PlanDeleteRequiresProductCancellation_WithdrawsUntouchedDrafts_AndRestoreDoesNotRecreateThem()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        var worker = await f.Store.Master("workers", new(null, "W", "Worker"), f.Actor);
        var plan = await f.Store.Plan(new(Guid.NewGuid(), family, new(2026, 10, 1), 2), f.Actor);
        var workspace = JsonSerializer.SerializeToElement(await f.Store.Workspace(true, planId: plan));
        var worked = workspace.GetProperty("products")[0].GetProperty("id").GetGuid();
        await f.Store.CorrectProduct(worked, new(worker, "작업자 지정"), f.Actor);
        var blocked = await Assert.ThrowsAsync<BusbarException>(() => f.Store.SetDeleted("plans", plan, "계획 삭제", f.Actor, true));
        Assert.Contains("제품을 먼저 취소", blocked.Message);
        await f.Store.CancelProduct(worked, new(Guid.NewGuid(), "제품 취소"), f.Actor);

        await f.Store.SetDeleted("plans", plan, "계획 삭제", f.Actor, true);
        Assert.Equal(2L, await f.Scalar("select count(*) from busbar_products where plan_id=(select id from busbar_plans limit 1) and status='Cancelled'"));
        var deletedWorkspace = JsonSerializer.SerializeToElement(await f.Store.Workspace(true));
        Assert.True(deletedWorkspace.GetProperty("plans")[0].GetProperty("isDeleted").GetBoolean());
        Assert.Equal(0, deletedWorkspace.GetProperty("productFamilies")[0].GetProperty("plannedQuantity").GetInt32());

        await f.Store.SetDeleted("plans", plan, "계획 복원", f.Actor, false);
        Assert.Equal(2L, await f.Scalar("select count(*) from busbar_products where status='Cancelled'"));
        Assert.Equal(2L, await f.Scalar("select count(*) from busbar_products"));
        await f.Store.Plan(new(plan, family, new(2026, 10, 1), 2), f.Actor);
        await f.Store.Plan(new(plan, family, new(2026, 10, 1), 2), f.Actor);
        Assert.Equal(2L, await f.Scalar("select count(*) from busbar_products where status='Draft'"));
        Assert.Equal(4L, await f.Scalar("select count(*) from busbar_products"));
        Assert.Equal(4L, await f.Scalar("select max(plan_sequence)::bigint from busbar_products"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task PurchaseAndMasterDeletionEnforceDependenciesAndKeepRowsForHistory()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        await f.Store.Settings(new("PJT"), f.Actor);
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        var material = await f.Store.Master("materials", new(null, "M", "Material", "개", "도급"), f.Actor);
        var worker = await f.Store.Master("workers", new(null, "W", "Worker"), f.Actor);
        var bom1 = await f.Store.Bom(new(family, [new(material, 1)]), f.Actor);
        var bom2 = await f.Store.Bom(new(family, [new(material, 2)]), f.Actor);
        await f.Store.SetDeleted("boms", bom1, "이전 버전 정리", f.Actor, true);
        await f.Store.SetDeleted("boms", bom1, "이전 버전 복원", f.Actor, false);
        await f.Store.SetDeleted("boms", bom2, "최신 미사용 버전 삭제", f.Actor, true);
        var product = await f.Store.Product(new(Guid.NewGuid(), family, worker), f.Actor);
        await f.Store.Photo(product, "front", [1], null, f.Actor);
        var tombstone = await Assert.ThrowsAsync<BusbarException>(() => f.Store.Photo(product, "back", [2], null, f.Actor));
        Assert.Contains("최신 표준 소요량이 삭제", tombstone.Message);
        await f.Store.CancelProduct(product, new(Guid.NewGuid(), "미완료 제품 취소"), f.Actor);
        await f.Store.SetDeleted("product-families", family, "제품군과 BOM 이력 보존", f.Actor, true);
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.SetDeleted("boms", bom2, "부모 삭제 중 복원", f.Actor, false));
        await f.Store.SetDeleted("product-families", family, "제품군 복원", f.Actor, false);
        await f.Store.SetDeleted("boms", bom2, "최신 BOM 복원", f.Actor, false);

        var purchaseMaterial = await f.Store.Master("materials", new(null, "PM", "Purchase material", "개", "도급"), f.Actor);
        var purchase = await f.Store.Purchase(new(null, "PO", purchaseMaterial, 5, new(2026, 10, 1)), f.Actor);
        var receipt = await f.Store.Receipt(new(Guid.NewGuid(), purchase, 5), f.Actor);
        var activeReceipt = await Assert.ThrowsAsync<BusbarException>(() => f.Store.SetDeleted("purchases", purchase, "발주 삭제", f.Actor, true));
        Assert.Contains("활성 입고", activeReceipt.Message);
        await f.Store.Reverse(receipt, new(Guid.NewGuid(), "입고 취소"), f.Actor);
        await f.Store.SetDeleted("purchases", purchase, "발주 삭제", f.Actor, true);
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Receipt(new(Guid.NewGuid(), purchase, 1), f.Actor));
        await f.Store.SetDeleted("purchases", purchase, "발주 복원", f.Actor, false);

        var spareFamily = await f.Store.Master("product-families", new(null, "SF", "Spare family"), f.Actor);
        await f.Store.SetDeleted("product-families", spareFamily, "미사용 정리", f.Actor, true);
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Product(new(Guid.NewGuid(), spareFamily, worker), f.Actor));
        await f.Store.SetDeleted("product-families", spareFamily, "복원", f.Actor, false);
        var spareMaterial = await f.Store.Master("materials", new(null, "SM", "Spare material", "개", "도급"), f.Actor);
        await f.Store.SetDeleted("materials", spareMaterial, "미사용 정리", f.Actor, true);
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Adjustment(new(Guid.NewGuid(), "Material", spareMaterial, 1, "조정"), f.Actor));
        await f.Store.SetDeleted("materials", spareMaterial, "복원", f.Actor, false);
        var spareWorker = await f.Store.Master("workers", new(null, "SW", "Spare worker"), f.Actor);
        await f.Store.SetDeleted("workers", spareWorker, "미사용 정리", f.Actor, true);
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.Product(new(Guid.NewGuid(), family, spareWorker), f.Actor));
        await f.Store.SetDeleted("workers", spareWorker, "복원", f.Actor, false);

        Assert.Equal(2L, await f.Scalar("select count(*) from busbar_boms"));
        Assert.Equal(1L, await f.Scalar("select count(*) from busbar_purchases"));
        Assert.Equal(1L, await f.Scalar("select count(*) from busbar_receipts"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task ConcurrentProjectDeleteAndWorkerClaimHaveOneSafeWinner()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family", EcountProductCode: "ERP-F", StandardUnitPrice: 100), f.Actor);
        await f.Store.Settings(new("PJT", "CUST", "WH"), f.Actor);
        for (var index = 0; index < 6; index++)
        {
            var project = await f.Store.Project(new(null, $"P{index}", "WO", family, 1, "Destination", new(2026, 10, 1)), f.Actor);
            var job = Job(await f.Store.EcountStatus(project)).GetProperty("id").GetGuid();
            BusbarEcountAttempt? attempt = null;
            BusbarException? deleteError = null;
            await Task.WhenAll(
                Task.Run(async () => attempt = await f.Store.ClaimEcountJob(job), TestContext.Current.CancellationToken),
                Task.Run(async () =>
                {
                    try { await f.Store.SetDeleted("projects", project, "경쟁 삭제", f.Actor, true); }
                    catch (BusbarException ex) { deleteError = ex; }
                }, TestContext.Current.CancellationToken));
            var deleted = await f.Scalar($"select count(*) from busbar_projects where id='{project}' and is_deleted");
            Assert.True((deleted == 1 && attempt is null && deleteError is null) || (deleted == 0 && attempt is not null && deleteError is not null));
            if (attempt is not null)
            {
                await f.Store.FinishEcountAttempt(attempt.Id, new("Failed"));
                await f.Store.SetDeleted("projects", project, "전송 실패 후 삭제", f.Actor, true);
            }
        }
    }

    private static JsonElement Job(object status) => JsonSerializer.SerializeToElement(status).GetProperty("jobs").EnumerateArray()
        .Single(job => job.GetProperty("kind").GetString() == "Order");
}
