using System.Text.Json;
using Emi.Qms.Api.InteriorBusbar;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarEcountQueueTests
{
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
