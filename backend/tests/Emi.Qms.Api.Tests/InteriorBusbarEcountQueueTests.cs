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
        await f.Store.FinishEcountAttempt(attempt.Id,new("Succeeded","SYN-ORDER"));
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
    public async Task ReviewedPartialShipmentFlagsFurtherPartialChangesWithoutResendingSale()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();var (_,project)=await Setup(f);
        var order=(await Job(f.Store,project,"Order")).GetProperty("id").GetGuid();
        var oa=(await f.Store.ClaimEcountJob(order))!;await f.Store.FinishEcountAttempt(oa.Id,new("Succeeded","SYN-O"));
        var shipment=await f.Store.Shipment(new(Guid.NewGuid(),project,60),f.Actor);
        var sale=(await Job(f.Store,project,"Sale")).GetProperty("id").GetGuid();
        var sa=(await f.Store.ClaimEcountJob(sale))!;await f.Store.FinishEcountAttempt(sa.Id,new("Succeeded","SYN-S"));
        await f.Store.Reverse(shipment,new(Guid.NewGuid(),"Synthetic correction"),f.Actor);
        await f.Store.Shipment(new(Guid.NewGuid(),project,20),f.Actor);
        await f.Store.ReconcileEcount(sale,new("Reviewed","Partial ERP correction checked"),f.Actor);
        Assert.False((await Job(f.Store,project,"Sale")).GetProperty("needsReview").GetBoolean());
        await f.Store.Settings(new("SYN-P","SYN-C","SYNWH"),f.Actor);
        Assert.False((await Job(f.Store,project,"Sale")).GetProperty("needsReview").GetBoolean());
        await f.Store.Shipment(new(Guid.NewGuid(),project,20),f.Actor);
        Assert.True((await Job(f.Store,project,"Sale")).GetProperty("needsReview").GetBoolean());
        Assert.Null(await f.Store.ClaimEcountJob(sale));
        Assert.Equal(2,await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }
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
    public async Task SplitShipmentsEnqueueOnceAndRecompletionReusesHeldSale()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var (_, project) = await Setup(f);
        Assert.Equal(1, await f.Scalar("select count(*) from busbar_ecount_jobs"));
        await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        Assert.Equal(1, await f.Scalar("select count(*) from busbar_ecount_jobs"));
        var request = new BusbarShipmentRequest(Guid.NewGuid(), project, 20);
        var last = await f.Store.Shipment(request, f.Actor);
        await f.Store.Shipment(request, f.Actor);
        var sale = await Job(f.Store, project, "Sale");
        Assert.Equal("Pending", sale.GetProperty("state").GetString());
        await f.Store.Reverse(last, new(Guid.NewGuid(), "Synthetic correction"), f.Actor);
        Assert.Equal("Held", (await Job(f.Store, project, "Sale")).GetProperty("state").GetString());
        await Assert.ThrowsAsync<BusbarException>(() => f.Store.RetryEcount(sale.GetProperty("id").GetGuid(), "Retry incomplete", f.Actor));
        await f.Store.Shipment(new(Guid.NewGuid(), project, 20), f.Actor);
        Assert.Equal(sale.GetProperty("id").GetGuid(), (await Job(f.Store, project, "Sale")).GetProperty("id").GetGuid());
        Assert.Equal(2, await f.Scalar("select count(*) from busbar_ecount_jobs"));
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
        await f.Store.FinishEcountAttempt(oa.Id, new("Succeeded", "SYN-ORDER"));
        var sa = (await f.Store.ClaimEcountJob(sale))!;
        await f.Store.FinishEcountAttempt(sa.Id, new("Succeeded", "SYN-SALE"));
        await f.Store.Reverse(shipment, new(Guid.NewGuid(), "Synthetic reversal"), f.Actor);
        await f.Store.Shipment(new(Guid.NewGuid(), project, 60), f.Actor);
        Assert.True((await Job(f.Store, project, "Sale")).GetProperty("needsReview").GetBoolean());
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
        await f.Store.FinishEcountAttempt(attempt.Id, new("Succeeded", "SYN-ORDER"));
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
}
