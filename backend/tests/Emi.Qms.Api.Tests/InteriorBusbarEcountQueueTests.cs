using System.Text.Json;
using Emi.Qms.Api.InteriorBusbar;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarEcountQueueTests
{
    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task CreatorAndEmployeeSnapshotSurviveEditsAndMappingChanges()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (family, project) = await Setup(f);
        var other = Guid.NewGuid();
        await using (var c = new Npgsql.NpgsqlConnection(f.Connection))
        {
            await c.OpenAsync(TestContext.Current.CancellationToken);
            await InteriorBusbarStore.Exec(c, "insert into qms_users(id,display_name) values(@id,'Different editor')", ("id",other));
            await InteriorBusbarStore.Exec(c, "update qms_users set display_name='Renamed creator' where id=@id", ("id",f.Actor));
        }
        await f.Store.Project(new(project,"Edited","SYN-WO",family,60,"Destination",new(2026,10,1),"Edit"),other);
        var job=(await Job(f.Store,project,"Order")).GetProperty("id").GetGuid();
        var attempt=(await f.Store.ClaimEcountJob(job))!;
        var frozen=JsonDocument.Parse(attempt.Payload).RootElement;
        Assert.Equal("Synthetic manager",frozen.GetProperty("registeredByName").GetString());
        Assert.Equal("SYN-EMP",frozen.GetProperty("employeeCode").GetString());
        await f.Store.FinishEcountAttempt(attempt.Id,new("Succeeded","2026/09/15 -1"));
        await f.Store.EcountEmployee(new(f.Actor,"SYN-NEW","Employee mapping corrected"),other);
        Assert.True((await Job(f.Store,project,"Order")).GetProperty("needsReview").GetBoolean());
        Assert.Null(await f.Store.ClaimEcountJob(job));
        Assert.Equal(1,await f.Scalar("select count(*) from busbar_ecount_attempts where payload->>'employeeCode'='SYN-EMP'"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task MissingEmployeeHoldsWithoutSendingAndCanBeConfigured()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (_,project)=await Setup(f);
        await using (var c=new Npgsql.NpgsqlConnection(f.Connection))
        {
            await c.OpenAsync(TestContext.Current.CancellationToken);
            await InteriorBusbarStore.Exec(c,"delete from busbar_ecount_employees");
        }
        var job=(await Job(f.Store,project,"Order")).GetProperty("id").GetGuid();
        Assert.Null(await f.Store.ClaimEcountJob(job));
        Assert.Equal("Held",(await Job(f.Store,project,"Order")).GetProperty("state").GetString());
        Assert.Equal(0,await f.Scalar("select count(*) from busbar_ecount_attempts"));
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.EcountEmployee(new(f.Actor,new string('X',31),"Invalid"),f.Actor));
        await f.Store.EcountEmployee(new(f.Actor,"SYN-EMP","Verified mapping"),f.Actor);
        await f.Store.RetryEcount(job,"Mapping supplied",f.Actor);
        Assert.NotNull(await f.Store.ClaimEcountJob(job));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task LegacyCreatorRecoveryRequiresUniqueCreationEvidence()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();
        var (family,good)=await Setup(f);
        var duplicate=await f.Store.Project(new(null,"Duplicate","",family,1,"D",new(2026,10,1)),f.Actor);
        var noAudit=await f.Store.Project(new(null,"No audit","",family,1,"D",new(2026,10,1)),f.Actor);
        await using var c=new Npgsql.NpgsqlConnection(f.Connection);
        await c.OpenAsync(TestContext.Current.CancellationToken);
        await InteriorBusbarStore.Exec(c,"update busbar_projects set registered_by=null,registered_by_name=null,registered_by_source=null");
        await InteriorBusbarStore.Exec(c,"delete from busbar_audit where entity_id=@id",("id",noAudit));
        await InteriorBusbarStore.Exec(c,"insert into busbar_audit select @newid,entity_kind,entity_id,reason,changed_by,changed_at_utc,before_value,after_value from busbar_audit where entity_id=@id",("newid",Guid.NewGuid()),("id",duplicate));
        var root=AppContext.BaseDirectory;
        while(!Directory.Exists(Path.Combine(root,"database","migrations"))) root=Directory.GetParent(root)!.FullName;
        var sql=await File.ReadAllTextAsync(Path.Combine(root,"database/migrations/0096_interior_busbar_project_creator.sql"),TestContext.Current.CancellationToken);
        await InteriorBusbarStore.Exec(c,sql[sql.IndexOf("with proven",StringComparison.Ordinal)..]);
        Assert.Equal(1,await f.Scalar("select count(*) from busbar_projects where registered_by is not null and registered_by_source='CreationAuditCurrentName'"));
        var restored=await InteriorBusbarStore.Rows(c,"select registered_by from busbar_projects where id=@id",("id",good));
        Assert.Equal(f.Actor,(Guid)restored[0]["registeredBy"]!);
    }

    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;
    private static async Task<(Guid family, Guid project)> Setup(InteriorBusbarStoreTests.Fixture f)
    {
        await f.Store.Settings(new("SYN-P", "SYN-C", "SYNWH"), f.Actor);
        var family = await f.Store.Master("product-families", new(null, "F", "Synthetic", EcountProductCode: "SYN-F", StandardUnitPrice: 100), f.Actor);
        var project = await f.Store.Project(new(null, "Synthetic", "SYN-WO", family, 60, "Synthetic", new(2026,10,1)), f.Actor);
        await f.Store.Adjustment(new(Guid.NewGuid(), "Finished", family, 60, "Synthetic opening", true), f.Actor);
        return (family, project);
    }
    private static async Task<JsonElement> Job(InteriorBusbarStore store, Guid project, string kind) =>
        JsonSerializer.SerializeToElement(await store.EcountStatus(project)).GetProperty("jobs").EnumerateArray().Single(j => j.GetProperty("kind").GetString() == kind);

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task EachPartialShipmentHasItsOwnFrozenLinkedSale()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (family, project) = await Setup(f);
        var order = (await Job(f.Store, project, "Order")).GetProperty("id").GetGuid();
        var oa = (await f.Store.ClaimEcountJob(order))!;
        await f.Store.FinishEcountAttempt(oa.Id, new("Succeeded", "2026/09/15 -7"));
        for (var i = 0; i < 3; i++)
        {
            var request = new BusbarShipmentRequest(Guid.NewGuid(), project, 20);
            var shipment = await f.Store.Shipment(request, f.Actor);
            Assert.Equal(shipment, await f.Store.Shipment(request, f.Actor));
            var sale = await ShipmentJob(f.Store, project, shipment);
            var claims = await Task.WhenAll(f.Store.ClaimEcountJob(sale.GetProperty("id").GetGuid()), f.Store.ClaimEcountJob(sale.GetProperty("id").GetGuid()));
            var attempt = Assert.Single(claims.OfType<BusbarEcountAttempt>());
            var payload = JsonDocument.Parse(attempt.Payload).RootElement;
            Assert.Equal(20, payload.GetProperty("quantity").GetInt32());
            Assert.Equal(2000m, payload.GetProperty("supplyAmount").GetDecimal());
            Assert.Equal(200m, payload.GetProperty("vatAmount").GetDecimal());
            Assert.Equal("20260915", payload.GetProperty("sourceOrderDate").GetString());
            Assert.Equal("7", payload.GetProperty("sourceOrderNumber").GetString());
            Assert.Equal(shipment, payload.GetProperty("shipmentId").GetGuid());
            await f.Store.FinishEcountAttempt(attempt.Id, new("Succeeded", $"2026/09/15 -{i + 10}"));
        }
        Assert.Equal(3, await f.Scalar("select count(*) from busbar_ecount_jobs where kind='Sale' and state='Succeeded' and not needs_review"));
        Assert.Equal(4, await f.Scalar("select count(*) from busbar_ecount_attempts"));
        Assert.Equal(0m, await f.Balance("Finished", family));
    }
    private static async Task<JsonElement> ShipmentJob(InteriorBusbarStore store, Guid project, Guid shipment) =>
        JsonSerializer.SerializeToElement(await store.EcountStatus(project)).GetProperty("jobs").EnumerateArray().Single(j => j.GetProperty("shipmentId").ValueKind != JsonValueKind.Null && j.GetProperty("shipmentId").GetGuid() == shipment);

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task ManualVerificationPreservesSnapshotAndAcknowledgedChangesOnly()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();
        var (family,project)=await Setup(f);var job=(await Job(f.Store,project,"Order")).GetProperty("id").GetGuid();
        var attempt=(await f.Store.ClaimEcountJob(job))!;
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ReconcileEcount(job,new("NotRecorded","Cannot reconcile inflight"),f.Actor));
        await f.Store.FinishEcountAttempt(attempt.Id,new("Unknown"));
        await f.Store.Master("product-families",new(family,"F","Synthetic",EcountProductCode:"SYN-F",StandardUnitPrice:200),f.Actor);
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ReconcileEcount(job,new("Recorded","Missing slip"),f.Actor));
        await f.Store.ReconcileEcount(job,new("Recorded","Verified existing ERP slip","SYN-SLIP"),f.Actor);
        Assert.True((await Job(f.Store,project,"Order")).GetProperty("needsReview").GetBoolean());
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ReconcileEcount(job,new("NotRecorded","Cannot undo confirmed slip"),f.Actor));
        await f.Store.ReconcileEcount(job,new("Reviewed","ERP amount manually corrected"),f.Actor);
        await f.Store.Settings(new("SYN-P","SYN-C","SYNWH"),f.Actor);
        Assert.False((await Job(f.Store,project,"Order")).GetProperty("needsReview").GetBoolean());
        Assert.Equal(100,await f.Scalar("select (payload->>'unitPrice')::numeric::bigint from busbar_ecount_attempts"));
        Assert.Equal(2,await f.Scalar("select count(*) from busbar_audit where entity_kind='EcountJob'"));
        Assert.Equal(1,await f.Scalar("select count(*) from busbar_ecount_runtime where paused"));
        await f.Store.Master("product-families",new(family,"F","Synthetic",EcountProductCode:"SYN-F",StandardUnitPrice:300),f.Actor);
        Assert.True((await Job(f.Store,project,"Order")).GetProperty("needsReview").GetBoolean());
        Assert.Null(await f.Store.ClaimEcountJob(job));
    }
    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task VerifiedAbsentSlipRequiresSeparateRetryAndWorkerLockRejectsReconciliation()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();var (_,project)=await Setup(f);
        var job=(await Job(f.Store,project,"Order")).GetProperty("id").GetGuid();var attempt=(await f.Store.ClaimEcountJob(job))!;
        await f.Store.FinishEcountAttempt(attempt.Id,new("Unknown"));
        await using(var connection=new Npgsql.NpgsqlConnection(f.Connection))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await InteriorBusbarStore.Exec(connection,"select pg_advisory_lock(9070095)");
            try { await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ReconcileEcount(job,new("NotRecorded","Worker busy"),f.Actor)); }
            finally { await InteriorBusbarStore.Exec(connection,"select pg_advisory_unlock(9070095)"); }
        }
        await f.Store.ReconcileEcount(job,new("NotRecorded","No matching ERP slip verified"),f.Actor);
        Assert.Equal("Failed",(await Job(f.Store,project,"Order")).GetProperty("state").GetString());
        Assert.Null(await f.Store.ClaimEcountJob(job));
        await f.Store.RetryEcount(job,"Verified retry",f.Actor);
        Assert.NotNull(await f.Store.ClaimEcountJob(job));
        Assert.Equal(2,await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }
    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task CancelledUnsentShipmentStaysHeldAndReplacementGetsNewJob()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (_, project) = await Setup(f);
        var request = new BusbarShipmentRequest(Guid.NewGuid(), project, 20);
        var shipment = await f.Store.Shipment(request, f.Actor);
        await f.Store.Shipment(request, f.Actor);
        var sale = await ShipmentJob(f.Store, project, shipment);
        await f.Store.Reverse(shipment, new(Guid.NewGuid(), "Synthetic correction"), f.Actor);
        Assert.Equal("Held", (await ShipmentJob(f.Store, project, shipment)).GetProperty("state").GetString());
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.RetryEcount(sale.GetProperty("id").GetGuid(), "Cancelled", f.Actor));
        var replacement = await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        Assert.NotEqual(sale.GetProperty("id").GetGuid(), (await ShipmentJob(f.Store, project, replacement)).GetProperty("id").GetGuid());
        Assert.Equal(3, await f.Scalar("select count(*) from busbar_ecount_jobs"));
        Assert.Equal(0, await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task ConcurrentClaimFreezesPriceAndUnknownIsNeverRetried()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (family, project) = await Setup(f);
        var job = (await Job(f.Store, project, "Order")).GetProperty("id").GetGuid();
        var claims = await Task.WhenAll(f.Store.ClaimEcountJob(job), f.Store.ClaimEcountJob(job));
        var attempt = Assert.Single(claims.OfType<BusbarEcountAttempt>());
        Assert.Equal(100m, JsonDocument.Parse(attempt.Payload).RootElement.GetProperty("unitPrice").GetDecimal());
        await f.Store.Master("product-families", new(family, "F", "Synthetic", EcountProductCode: "SYN-F", StandardUnitPrice: 200), f.Actor);
        Assert.True((await Job(f.Store, project, "Order")).GetProperty("needsReview").GetBoolean());
        await f.Store.FinishEcountAttempt(attempt.Id, new("Unknown"));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.RetryEcount(job, "Unsafe repeat", f.Actor));
        Assert.Null(await f.Store.ClaimEcountJob(job));
        Assert.Equal(100, await f.Scalar("select (payload->>'unitPrice')::numeric::bigint from busbar_ecount_attempts"));
        Assert.Equal(1, await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task DefiniteFailureCanRetryAndLateResultCannotOverwriteNewAttempt()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (family, project) = await Setup(f);
        var job = (await Job(f.Store, project, "Order")).GetProperty("id").GetGuid();
        var first = (await f.Store.ClaimEcountJob(job))!;
        await f.Store.Master("product-families", new(family, "F", "Synthetic", EcountProductCode: "SYN-F", StandardUnitPrice: 200), f.Actor);
        await f.Store.FinishEcountAttempt(first.Id, new("Failed"));
        Assert.False((await Job(f.Store, project, "Order")).GetProperty("needsReview").GetBoolean());
        await f.Store.RetryEcount(job, "Synthetic validation corrected", f.Actor);
        var second = (await f.Store.ClaimEcountJob(job))!;
        Assert.False(await f.Store.FinishEcountAttempt(first.Id, new("Succeeded", "OLD")));
        Assert.True(await f.Store.FinishEcountAttempt(second.Id, new("Succeeded", "SYN-SLIP")));
        Assert.Equal("SYN-SLIP", (await Job(f.Store, project, "Order")).GetProperty("slipNumber").GetString());
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.RetryEcount(job, "Duplicate success", f.Actor));
        Assert.Equal(2, await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task SaleWaitsForOrderAndSentSaleNeverResendsAfterReversal()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (_, project) = await Setup(f);
        var shipment = await f.Store.Shipment(new(Guid.NewGuid(), project, 60), f.Actor);
        var sale = (await Job(f.Store, project, "Sale")).GetProperty("id").GetGuid();
        Assert.Null(await f.Store.ClaimEcountJob(sale));
        var order = (await Job(f.Store, project, "Order")).GetProperty("id").GetGuid();
        var oa = (await f.Store.ClaimEcountJob(order))!;
        await f.Store.FinishEcountAttempt(oa.Id, new("Succeeded", "2026/09/15 -1"));
        var sa = (await f.Store.ClaimEcountJob(sale))!;
        await f.Store.FinishEcountAttempt(sa.Id, new("Succeeded", "SYN-SALE"));
        await f.Store.Reverse(shipment, new(Guid.NewGuid(), "Synthetic reversal"), f.Actor);
        await f.Store.Shipment(new(Guid.NewGuid(), project, 60), f.Actor);
        Assert.True((await ShipmentJob(f.Store, project, shipment)).GetProperty("needsReview").GetBoolean());
        Assert.Null(await f.Store.ClaimEcountJob(sale));
        Assert.Equal(2, await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task InterruptedAttemptBecomesUnknownAndMissingDataDoesNotCreateAttempt()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (_, project) = await Setup(f);
        var job = (await Job(f.Store, project, "Order")).GetProperty("id").GetGuid();
        await f.Store.Settings(new("SYN-P", "", "SYNWH"), f.Actor);
        Assert.Null(await f.Store.ClaimEcountJob(job));
        Assert.Equal(0, await f.Scalar("select count(*) from busbar_ecount_attempts"));
        await f.Store.Settings(new("SYN-P", "SYN-C", "SYNWH"), f.Actor);
        await f.Store.RetryEcount(job, "Configured customer", f.Actor);
        await f.Store.ClaimEcountJob(job);
        f.Clock.Now = f.Clock.Now.AddMinutes(6);
        await f.Store.Settings(new("SYN-P", "SYN-C2", "SYNWH"), f.Actor);
        Assert.Equal(1, await f.Store.RecoverEcountAttempts());
        Assert.Equal("Unknown", (await Job(f.Store, project, "Order")).GetProperty("state").GetString());
        Assert.Null(await f.Store.ClaimEcountJob(job));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task UnchangedSavesAndNewProjectCommonCodeDoNotInvalidateSentOrder()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (family, project) = await Setup(f);
        var job = (await Job(f.Store, project, "Order")).GetProperty("id").GetGuid();
        var attempt = (await f.Store.ClaimEcountJob(job))!;
        await f.Store.FinishEcountAttempt(attempt.Id, new("Succeeded", "2026/09/15 -1"));
        await f.Store.Master("product-families", new(family, "F", "Synthetic", EcountProductCode: "SYN-F", StandardUnitPrice: 100), f.Actor);
        await f.Store.Settings(new("NEW-COMMON", "SYN-C", "SYNWH"), f.Actor);
        await f.Store.Project(new(project, "Synthetic", "SYN-WO", family, 60, "Synthetic", new(2026,10,1), "No commercial changes"), f.Actor);
        Assert.False((await Job(f.Store, project, "Order")).GetProperty("needsReview").GetBoolean());
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task DecimalAmountsRemainExactAndUnsupportedPrecisionIsHeld()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (family, project) = await Setup(f);
        var job = (await Job(f.Store, project, "Order")).GetProperty("id").GetGuid();
        await f.Store.Project(new(project, "Synthetic", "SYN-WO", family, 1, "Synthetic", new(2026,10,1), "One item"), f.Actor);
        await f.Store.Master("product-families", new(family, "F", "Synthetic", EcountProductCode: "SYN-F", StandardUnitPrice: 0.0001m), f.Actor);
        Assert.Null(await f.Store.ClaimEcountJob(job));
        Assert.Equal(0, await f.Scalar("select count(*) from busbar_ecount_attempts"));
        await f.Store.Master("product-families", new(family, "F", "Synthetic", EcountProductCode: "SYN-F", StandardUnitPrice: 12345m), f.Actor);
        await f.Store.RetryEcount(job, "Supported price", f.Actor);
        var attempt = (await f.Store.ClaimEcountJob(job))!;
        var payload = JsonDocument.Parse(attempt.Payload).RootElement;
        Assert.Equal(1234.5m, payload.GetProperty("vatAmount").GetDecimal());
        Assert.Equal(13579.5m, payload.GetProperty("totalAmount").GetDecimal());
    }
    [Theory]
    [InlineData("2026/09/15 -3", "20260915", "3")]
    [InlineData("20260915-12", "20260915", "12")]
    [InlineData("2026/02/30 -1", null, null)]
    [InlineData("2026/09/15 -0", null, null)]
    [InlineData("SYN-ORDER", null, null)]
    public void OriginalOrderSlipRequiresDateAndNumber(string value, string? date, string? number)
    {
        var parsed = InteriorBusbarStore.ParseOrderSlip(value);
        Assert.Equal(date, parsed?.Date); Assert.Equal(number, parsed?.Number);
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task MissingOrMalformedOrderCannotSendSaleAndUnknownSaleBlocksNext()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (_, project) = await Setup(f);
        var first = await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        var sale = (await ShipmentJob(f.Store, project, first)).GetProperty("id").GetGuid();
        var order = (await Job(f.Store, project, "Order")).GetProperty("id").GetGuid();
        var oa = (await f.Store.ClaimEcountJob(order))!;
        await f.Store.FinishEcountAttempt(oa.Id, new("Succeeded", "INVALID"));
        Assert.Null(await f.Store.ClaimEcountJob(sale));
        await using var c = new Npgsql.NpgsqlConnection(f.Connection); await c.OpenAsync(TestContext.Current.CancellationToken);
        await InteriorBusbarStore.Exec(c, "update busbar_ecount_jobs set slip_number='2026/09/15 -1' where id=@id", ("id", order));
        await f.Store.RetryEcount(sale, "Synthetic slip correction", f.Actor);
        var sa = (await f.Store.ClaimEcountJob(sale))!;
        await f.Store.FinishEcountAttempt(sa.Id, new("Unknown"));
        var second = await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        var next = (await ShipmentJob(f.Store, project, second)).GetProperty("id").GetGuid();
        Assert.Null(await f.Store.ClaimEcountJob(next));
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.RetryEcount(sale, "Unsafe repeat", f.Actor));
        Assert.Equal(2, await f.Scalar("select count(*) from busbar_ecount_attempts"));
        await f.Store.ReconcileEcount(sale, new("Recorded", "Synthetic ERP verified", "2026/09/15 -9"), f.Actor);
        await f.Store.RetryEcount(next, "Previous sale verified", f.Actor);
        Assert.NotNull(await f.Store.ClaimEcountJob(next));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task CancelDuringFlightPreservesReviewAndRequiresReconciliationBeforeNextSale()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (_, project) = await Setup(f);
        var order = (await Job(f.Store, project, "Order")).GetProperty("id").GetGuid();
        var oa = (await f.Store.ClaimEcountJob(order))!;
        await f.Store.FinishEcountAttempt(oa.Id, new("Succeeded", "2026/09/15 -1"));
        var shipment = await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        var sale = (await ShipmentJob(f.Store, project, shipment)).GetProperty("id").GetGuid();
        var sa = (await f.Store.ClaimEcountJob(sale))!;
        await f.Store.Reverse(shipment, new(Guid.NewGuid(), "Cancelled during send"), f.Actor);
        await f.Store.FinishEcountAttempt(sa.Id, new("Succeeded", "2026/09/15 -9"));
        Assert.True((await ShipmentJob(f.Store, project, shipment)).GetProperty("needsReview").GetBoolean());
        var replacement = await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        var next = (await ShipmentJob(f.Store, project, replacement)).GetProperty("id").GetGuid();
        Assert.Null(await f.Store.ClaimEcountJob(next));
        await f.Store.ReconcileEcount(sale, new("Reviewed", "Cancelled sale corrected in ERP"), f.Actor);
        await f.Store.RetryEcount(next, "Correction checked", f.Actor);
        Assert.NotNull(await f.Store.ClaimEcountJob(next));
        Assert.Null(await f.Store.ClaimEcountJob(sale));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task MigrationPreservesLegacySalesAndDoesNotBackfillShipments()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create(applyShipmentMigration: false);
        await using var c = new Npgsql.NpgsqlConnection(f.Connection); await c.OpenAsync(TestContext.Current.CancellationToken);
        var family = Guid.NewGuid(); var project = Guid.NewGuid(); var sale = Guid.NewGuid();
        await InteriorBusbarStore.Exec(c, "insert into busbar_product_families(id,code,name) values(@id,'SYN','Synthetic')", ("id",family));
        await InteriorBusbarStore.Exec(c, "insert into busbar_projects(id,name,customer_job_number,common_project_code,product_family_id,requested_quantity,destination,due_date) values(@id,'Synthetic','','SYN',@family,60,'SYN','2026-10-01')", ("id",project),("family",family));
        await InteriorBusbarStore.Exec(c, "insert into busbar_ecount_jobs(id,project_id,kind) values(@id,@project,'Sale')", ("id",sale),("project",project));
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root,"database","migrations"))) root = Directory.GetParent(root)!.FullName;
        await InteriorBusbarStore.Exec(c, await File.ReadAllTextAsync(Path.Combine(root,"database/migrations/0097_interior_busbar_shipment_sales.sql"), TestContext.Current.CancellationToken));
        Assert.Equal(1, await f.Scalar("select count(*) from busbar_ecount_jobs where kind='Sale' and state='Held' and needs_review and shipment_id is null"));
        Assert.Null(await f.Store.ClaimEcountJob(sale));
        Assert.Equal(0, await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }

}
