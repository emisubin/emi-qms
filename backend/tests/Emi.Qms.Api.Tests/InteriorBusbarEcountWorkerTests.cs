using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.InteriorBusbar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarEcountWorkerTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;
    private static InteriorBusbarEcountOptions Options(string environment = "Test", bool enabled = true, string company = "SYN001") => InteriorBusbarEcountOptions.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["InteriorBusbar:Ecount:Enabled"] = enabled.ToString(), ["InteriorBusbar:Ecount:Environment"] = environment,
        ["InteriorBusbar:Ecount:CompanyCode"] = company, ["InteriorBusbar:Ecount:UserId"] = "Synthetic",
        ["InteriorBusbar:Ecount:ApiKey"] = "synthetic-not-a-real-key", ["InteriorBusbar:Ecount:SessionIdleMinutes"] = "30"
    }).Build());
    private static DatabaseConnectionStringProvider Connections(string? connection) => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:QmsDatabase"]=connection}).Build());
    private static InteriorBusbarEcountWorker Worker(InteriorBusbarStoreTests.Fixture f, FakeClient client, InteriorBusbarEcountOptions? options = null) => new(Connections(f.Connection),options??Options(),client,f.Clock,NullLogger<InteriorBusbarEcountWorker>.Instance);
    private static async Task Identity(InteriorBusbarStoreTests.Fixture f, string code = "CHEONGJU")
    {
        await using var connection = new NpgsqlConnection(f.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("create table qms_database_identity(singleton bool,database_kind text,business_unit_code text,schema_contract text);insert into qms_database_identity values(true,'business',@code,@schema)",connection);
        command.Parameters.AddWithValue("code",code);command.Parameters.AddWithValue("schema",BusinessUnitConfiguration.BusinessSchemaVersion);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
    private static async Task<Guid> Project(InteriorBusbarStoreTests.Fixture f)
    {
        await f.Store.Settings(new("SYN-P", "SYN-C", "SYNWH"), f.Actor);
        var family = await f.Store.Master("product-families",new(null,Guid.NewGuid().ToString(),"Synthetic",EcountProductCode:"SYN-F",StandardUnitPrice:12345),f.Actor);
        return await f.Store.Project(new(null,"Synthetic","SYN-WO",family,1,"Synthetic",new(2026,10,1)),f.Actor);
    }
    [Fact]
    public async Task DisabledDoesNotOpenDatabaseOrCallProvider()
    {
        var client = new FakeClient();
        using var worker = new InteriorBusbarEcountWorker(Connections(null),Options(enabled:false),client,TimeProvider.System,NullLogger<InteriorBusbarEcountWorker>.Instance);
        Assert.False(await worker.RunOnceAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0,client.Logins); Assert.Equal(0,client.Sends);
    }
    [Theory(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    [InlineData("OSAN")]
    [InlineData("DIRECTORY")]
    public async Task WrongIdentityCannotAuthenticateEvenWithLegacyConfiguration(string code)
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Identity(f,code);await Project(f);
        var client=new FakeClient();using var worker=Worker(f,client);
        await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(()=>worker.RunOnceAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0,client.Logins);Assert.Equal(0,client.Sends);
        Assert.Equal(0,await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }
    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task MissingIdentityCannotCallProvider()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Project(f);
        var client=new FakeClient();using var worker=Worker(f,client);
        await Assert.ThrowsAsync<PostgresException>(()=>worker.RunOnceAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0,client.Logins);Assert.Equal(0,client.Sends);
    }
    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task LoginFailurePersistsPauseAcrossWorkersAndNoAttemptsAreCreated()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Identity(f);await Project(f);
        var client=new FakeClient{LoginSuccess=false};using var worker=Worker(f,client);
        Assert.False(await worker.RunOnceAsync(TestContext.Current.CancellationToken));
        using var next=Worker(f,client);Assert.False(await next.RunOnceAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1,client.Logins);Assert.Equal(0,client.Sends);
        Assert.Equal(1,await f.Scalar("select count(*) from busbar_ecount_runtime where paused"));
        Assert.Equal(0,await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }
    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task SessionAndPersistentPacingSerializeSendsAndBindEnvironment()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Identity(f);await Project(f);await Project(f);
        var client=new FakeClient();using var worker=Worker(f,client);
        Assert.True(await worker.RunOnceAsync(TestContext.Current.CancellationToken));
        Assert.False(await worker.RunOnceAsync(TestContext.Current.CancellationToken));
        f.Clock.Now=f.Clock.Now.AddSeconds(12);
        Assert.True(await worker.RunOnceAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1,client.Logins);Assert.Equal(2,client.Sends);
        Assert.Equal(2,await f.Scalar("select count(*) from busbar_ecount_jobs where state='Succeeded'"));
        using var production=Worker(f,client,Options("Production"));
        Assert.False(await production.RunOnceAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2,client.Sends);
    }
    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task UnknownStopsOtherJobsAndExplicitResumeNeverRetriesUncertainJob()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Identity(f);await Project(f);await Project(f);
        var client=new FakeClient{Result=new("Unknown")};using var worker=Worker(f,client);
        await worker.RunOnceAsync(TestContext.Current.CancellationToken);f.Clock.Now=f.Clock.Now.AddSeconds(12);
        Assert.False(await worker.RunOnceAsync(TestContext.Current.CancellationToken));
        var store=new InteriorBusbarStore(Connections(f.Connection),f.Clock,ecountOptions:Options());
        await store.ResumeEcount("Reviewed connection only",f.Actor);
        client.Result=new("Succeeded","SYN-SLIP");await worker.RunOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2,client.Sends);
        Assert.Equal(1,await f.Scalar("select count(*) from busbar_ecount_jobs where state='Unknown'"));
        Assert.Equal(2,await f.Scalar("select count(*) from busbar_ecount_attempts"));
    }
    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task ResumeIsRejectedDuringAuthenticationAndConcurrentWorkerDoesNotLogin()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Identity(f);await Project(f);
        var client=new BlockingClient();using var first=new InteriorBusbarEcountWorker(Connections(f.Connection),Options(),client,f.Clock,NullLogger<InteriorBusbarEcountWorker>.Instance);
        var running=first.RunOnceAsync(TestContext.Current.CancellationToken);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(10),TestContext.Current.CancellationToken);
        try
        {
            var store=new InteriorBusbarStore(Connections(f.Connection),f.Clock,ecountOptions:Options());
            await Assert.ThrowsAsync<BusbarException>(()=>store.ResumeEcount("Race",f.Actor));
            var secondClient=new FakeClient();using var second=Worker(f,secondClient);
            Assert.False(await second.RunOnceAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0,secondClient.Logins);
        }
        finally {client.Release.TrySetResult(false);}
        Assert.False(await running);
        Assert.Equal(1,await f.Scalar("select count(*) from busbar_ecount_runtime where paused"));
    }
    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task OutcomeAndRecoveryCommitTheirPauseWithoutWorkerFollowup()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Identity(f);await Project(f);
        var status=System.Text.Json.JsonSerializer.SerializeToElement(await f.Store.Workspace(true));
        var project=status.GetProperty("projects")[0].GetProperty("id").GetGuid();
        var jobs=System.Text.Json.JsonSerializer.SerializeToElement(await f.Store.EcountStatus(project));
        var job=jobs.GetProperty("jobs")[0].GetProperty("id").GetGuid();
        var attempt=(await f.Store.ClaimEcountJob(job))!;
        await f.Store.FinishEcountAttempt(attempt.Id,new("Unknown"));
        Assert.Equal(1,await f.Scalar("select count(*) from busbar_ecount_runtime where paused"));
        var store=new InteriorBusbarStore(Connections(f.Connection),f.Clock,ecountOptions:Options());
        await store.ResumeEcount("Reviewed connection",f.Actor);
        var next=await Project(f);
        var nextJobs=System.Text.Json.JsonSerializer.SerializeToElement(await f.Store.EcountStatus(next));
        await f.Store.ClaimEcountJob(nextJobs.GetProperty("jobs")[0].GetProperty("id").GetGuid());
        f.Clock.Now=f.Clock.Now.AddMinutes(6);await f.Store.RecoverEcountAttempts();
        Assert.Equal(1,await f.Scalar("select count(*) from busbar_ecount_runtime where paused"));
    }
    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task RealQueueSnapshotPassesThroughHttpAdapterWithExactDecimalAmounts()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Identity(f);await Project(f);
        using var handler=new WireHandler();using var http=new HttpClient(handler);
        var adapter=new InteriorBusbarEcountClient(Options(),f.Clock,http);
        using var worker=new InteriorBusbarEcountWorker(Connections(f.Connection),Options(),adapter,f.Clock,NullLogger<InteriorBusbarEcountWorker>.Instance);
        Assert.True(await worker.RunOnceAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3,handler.Requests);
        Assert.Equal(1,await f.Scalar("select count(*) from busbar_ecount_jobs where state='Succeeded'"));
    }
    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task ProductionLoginSpacingSurvivesExplicitResumeAndRestart()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Identity(f);await Project(f);
        var options=Options("Production");var client=new FakeClient{LoginSuccess=false};using var worker=Worker(f,client,options);
        Assert.False(await worker.RunOnceAsync(TestContext.Current.CancellationToken));
        var store=new InteriorBusbarStore(Connections(f.Connection),f.Clock,ecountOptions:options);
        await store.ResumeEcount("Synthetic settings checked",f.Actor);
        f.Clock.Now=f.Clock.Now.AddMinutes(9);
        var nextClient=new FakeClient();using var next=Worker(f,nextClient,options);
        Assert.False(await next.RunOnceAsync(TestContext.Current.CancellationToken));Assert.Equal(0,nextClient.Logins);
        f.Clock.Now=f.Clock.Now.AddMinutes(1);
        Assert.True(await next.RunOnceAsync(TestContext.Current.CancellationToken));Assert.Equal(1,nextClient.Logins);
    }
    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task ThirdDefiniteFailurePausesAndCompanyChangeCannotSend()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();await Identity(f);
        for(var i=0;i<4;i++) await Project(f);
        var client=new FakeClient{Result=new("Failed")};using var worker=Worker(f,client);
        for(var i=0;i<3;i++){Assert.True(await worker.RunOnceAsync(TestContext.Current.CancellationToken));f.Clock.Now=f.Clock.Now.AddSeconds(12);}
        Assert.False(await worker.RunOnceAsync(TestContext.Current.CancellationToken));Assert.Equal(3,client.Sends);
        var store=new InteriorBusbarStore(Connections(f.Connection),f.Clock,ecountOptions:Options());
        await store.ResumeEcount("Checked",f.Actor);
        var different=new FakeClient();using var other=Worker(f,different,Options(company:"SYN002"));
        Assert.False(await other.RunOnceAsync(TestContext.Current.CancellationToken));Assert.Equal(0,different.Logins);Assert.Equal(0,different.Sends);
    }
    private sealed class BlockingClient:IInteriorBusbarEcountClient
    {
        public bool HasSession=>false;
        public TaskCompletionSource<bool> Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken){Started.TrySetResult(true);return await Release.Task.WaitAsync(cancellationToken);}
        public Task<BusbarEcountResult> SendAsync(BusbarEcountAttempt attempt,CancellationToken cancellationToken)=>throw new InvalidOperationException();
    }
    private sealed class WireHandler:HttpMessageHandler
    {
        public int Requests {get;private set;}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Requests++;
            string response;
            if(request.RequestUri!.AbsolutePath.EndsWith("/Zone",StringComparison.Ordinal)) response="""{"Status":"200","Error":null,"Data":{"ZONE":"CB","DOMAIN":".ecount.com"}}""";
            else if(request.RequestUri.AbsolutePath.EndsWith("/OAPILogin",StringComparison.Ordinal)) response="""{"Status":"200","Error":null,"Data":{"Code":"00","Datas":{"COM_CODE":"SYN001","USER_ID":"Synthetic","SESSION_ID":"synthetic-session"}}}""";
            else
            {
                var payload=System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var row=payload.RootElement.GetProperty("SaleOrderList")[0].GetProperty("BulkDatas");
                Assert.Equal("20261001",row.GetProperty("TIME_DATE").GetString());
                Assert.Equal(1234.5m,row.GetProperty("VAT_AMT").GetDecimal());
                Assert.Equal("SYN-WO",row.GetProperty("U_MEMO2").GetString());
                Assert.False(row.TryGetProperty("U_MEMO1",out _));
                response="""{"Status":"200","Error":null,"Data":{"SuccessCnt":1,"FailCnt":0,"ResultDetails":[{"IsSuccess":true,"Errors":[]}],"SlipNos":["SYN-ORDER"]}}""";
            }
            return new(System.Net.HttpStatusCode.OK){Content=new StringContent(response)};
        }
    }
    private sealed class FakeClient : IInteriorBusbarEcountClient
    {
        public bool HasSession {get;private set;}
        public bool LoginSuccess {get;init;}=true;
        public int Logins {get;private set;}
        public int Sends {get;private set;}
        public BusbarEcountResult Result {get;set;}=new("Succeeded","SYN-SLIP");
        public Task<bool> AuthenticateAsync(CancellationToken cancellationToken){Logins++;HasSession=LoginSuccess;return Task.FromResult(LoginSuccess);}
        public Task<BusbarEcountResult> SendAsync(BusbarEcountAttempt attempt,CancellationToken cancellationToken){Sends++;return Task.FromResult(Result);}
    }
}
