using System.Net;
using ImageMagick;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.InteriorBusbar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarAuthorizationTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;

    [Fact]
    public async Task AnonymousCannotAccessWorkspace()
    {
        using var factory = new QmsWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/interior-busbar/workspace", TestContext.Current.CancellationToken)).StatusCode);
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/interior-busbar", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(routes);
        Assert.All(routes, route => Assert.NotEmpty(route.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Theory]
    [InlineData("dev-sales")]
    [InlineData("dev-manufacturing")]
    [InlineData("dev-logistics")]
    [InlineData("dev-viewer")]
    public async Task ExistingDepartmentRolesCannotWriteBusbar(string key)
    {
        using var factory = new QmsWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, key);
        var response = await client.PostAsJsonAsync("/api/interior-busbar/adjustments", new BusbarAdjustmentRequest(Guid.NewGuid(), "Finished", Guid.NewGuid(), 1, "Synthetic", true), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/interior-busbar/access")]
    [InlineData("GET", "/api/interior-busbar/masters")]
    [InlineData("GET", "/api/interior-busbar/master-access")]
    [InlineData("GET", "/api/interior-busbar/workspace")]
    [InlineData("GET", "/api/interior-busbar/projects/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/interior-busbar/projects/00000000-0000-0000-0000-000000000001/scan?code=IB-00000001")]
    [InlineData("GET", "/api/interior-busbar/products/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/interior-busbar/workers")]
    [InlineData("GET", "/api/interior-busbar/projects/00000000-0000-0000-0000-000000000001/ecount-status")]
    [InlineData("POST", "/api/interior-busbar/ecount-jobs/00000000-0000-0000-0000-000000000001/retry")]
    [InlineData("POST", "/api/interior-busbar/ecount/resume")]
    [InlineData("POST", "/api/interior-busbar/ecount-jobs/00000000-0000-0000-0000-000000000001/reconcile")]
    public async Task TrustedOsanContextCannotReadOrWriteCheongjuModule(string method, string path)
    {
        var http = new DefaultHttpContext();
        var osan = new BusinessUnitDatabaseTarget(BusinessUnitCodes.Osan, BusinessUnitDatabaseKind.Business,
            "NeverConnect", "NeverConnect", "NeverConnect", "NeverConnect", "", "", "", false, false, false);
        BusinessUnitRequestContextFeature.Set(http, new(BusinessUnitAccessStatuses.Selected, Guid.NewGuid(), osan, [BusinessUnitCodes.Osan], false, "test"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BusinessUnits:Enabled"] = "true" }).Build();
        using var factory = QmsWebApplicationFactory.Create("Testing", new Dictionary<string, string?> {
            ["DevAuthentication:Enabled"] = "true", ["Database:ApplyMigrationsOnStartup"] = "false" },
            configureTestServices: services =>
            {
                services.AddSingleton(new DatabaseConnectionStringProvider(config, new FixedContextAccessor { HttpContext = http }));
                services.AddAuthentication(o => { o.DefaultAuthenticateScheme = "BusbarTest"; o.DefaultChallengeScheme = "BusbarTest"; })
                    .AddScheme<AuthenticationSchemeOptions, OsanAuthentication>("BusbarTest", _ => { });
            });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-admin");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new BusbarMasterRequest(null, "W", "Worker"));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, string.Join("\n", factory.Logs.Entries.Select(x => x.Message + " " + x.Exception)));
        Assert.Contains("business_unit_capability_disabled", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void OverallAdministratorAlwaysHasMasterAccessWithoutDesignation()
    {
        var profile = new UserAuthorizationProfile(new QmsUser(Guid.NewGuid(),"synthetic","Synthetic",null,true),null,[],[],[]);
        var access = BusbarAccess.For(profile, true);
        Assert.True(access.MastersRead && access.MastersWrite && access.ManageMasterPermissions);
        Assert.True(access.Allows("/api/interior-busbar/master-access"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task DedicatedManagerCanWrite_ReadOnlyCanRead_RevokedRoleIsImmediatelyDenied()
    {
        await using var fixture = await InteriorBusbarStoreTests.Fixture.Create();
        var identity = new MutableIdentity(fixture.Actor);
        using var factory = QmsWebApplicationFactory.Create("Testing", new Dictionary<string, string?>
        {
            ["DevAuthentication:Enabled"] = "true",
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["DevelopmentData:SeedEnabled"] = "false",
            ["ConnectionStrings:QmsDatabase"] = fixture.Connection,
            ["InteriorBusbar:Publication:Enabled"] = "false"
        }, identityStore: identity);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "busbar-fixture");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/interior-busbar/workers", new BusbarMasterRequest(null, "W", "Worker"), TestContext.Current.CancellationToken)).StatusCode);
        var family = await fixture.Store.Master("product-families", new(null, "F", "Synthetic"), fixture.Actor);
        await fixture.Store.Settings(new("SYN-P"), fixture.Actor);
        var project = await fixture.Store.Project(new(null, "Synthetic", "", family, 1, "Synthetic", new(2026,10,1)), fixture.Actor);
        identity.Manager = false;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/interior-busbar/workspace", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/interior-busbar/workers", new BusbarMasterRequest(null, "W2", "Worker"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/interior-busbar/projects/{project}/ecount-status", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/interior-busbar/ecount-jobs/{Guid.NewGuid()}/retry", new BusbarEcountRetryRequest("Synthetic"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/interior-busbar/ecount/resume", new BusbarEcountRetryRequest("Synthetic"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/interior-busbar/ecount-jobs/{Guid.NewGuid()}/reconcile", new BusbarEcountReconcileRequest("NotRecorded","Synthetic"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(1L, await fixture.Scalar("select count(*) from busbar_workers"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task RealHttpPhotoRegistrationCompletesOnceAndBlocksQrUntilPublished()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var productionIdentity = new MutableIdentity(f.Actor);
        using var factory = QmsWebApplicationFactory.Create("Testing", new Dictionary<string, string?> {
            ["DevAuthentication:Enabled"] = "true", ["Database:ApplyMigrationsOnStartup"] = "false",
            ["ConnectionStrings:QmsDatabase"] = f.Connection, ["InteriorBusbar:Publication:Enabled"] = "false"
        }, identityStore: productionIdentity);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "busbar-fixture");
        async Task<Guid> PostId(string path, object payload)
        {
            using var response = await client.PostAsJsonAsync("/api/interior-busbar" + path, payload, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(TestContext.Current.CancellationToken);
            return body!["id"];
        }
        var family = await PostId("/product-families", new BusbarMasterRequest(null, "F", "Family"));
        var material = await PostId("/materials", new BusbarMasterRequest(null, "M", "Material", "m", "도급"));
        var worker = await PostId("/workers", new BusbarMasterRequest(null, "W", "Worker"));
        await PostId("/boms", new BusbarBomRequest(family, [new(material, 2)]));
        productionIdentity.Manager = false;
        productionIdentity.DepartmentCode = "production-planning";
        var plan = await PostId("/plans", new BusbarPlanRequest(Guid.NewGuid(), family, DateOnly.FromDateTime(DateTime.UtcNow), 3));
        Guid product;
        await using (var connection = new Npgsql.NpgsqlConnection(f.Connection))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new Npgsql.NpgsqlCommand("select id from busbar_products where plan_id=@plan order by plan_sequence limit 1", connection);
            command.Parameters.AddWithValue("plan", plan);
            product = (Guid)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        }
        var prepared = await f.Store.GetProduct(product);
        Assert.Null(prepared["workerId"]);
        Assert.Null(prepared["workerName"]);
        Assert.Null(prepared["number"]);
        Assert.Equal(3L, await f.Scalar("select count(*) from busbar_products where status='Draft'"));
        using var image = new MagickImage(MagickColors.SteelBlue, 32, 32);
        var bytes = image.ToByteArray(MagickFormat.Png);
        async Task<HttpResponseMessage> Photo(string side, byte[] content, bool selectWorker = true)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(content), "file", "synthetic.png");
            if (selectWorker) form.Add(new StringContent(worker.ToString()), "workerId");
            return await client.PutAsync($"/api/interior-busbar/products/{product}/photos/{side}", form, TestContext.Current.CancellationToken);
        }
        productionIdentity.DepartmentCode = "manufacturing";
        using var invalid = await Photo("front", [1, 2, 3]);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var noWorker = await Photo("front", bytes, false);
        Assert.Equal(HttpStatusCode.BadRequest, noWorker.StatusCode);
        Assert.Equal(0L, await f.Scalar("select count(*) from busbar_photos"));
        using var first = await Photo("front", bytes);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("Draft", (await f.Store.GetProduct(product))["status"]);
        Assert.Equal(0m, await f.Balance("Finished", family));
        var before = DateTimeOffset.UtcNow;
        using var second = await Photo("back", bytes);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var completed = await f.Store.GetProduct(product);
        Assert.Equal("Complete", completed["status"]);
        using var detail = await client.GetAsync($"/api/interior-busbar/products/{product}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailJson = await detail.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(detailJson.GetProperty("hasFront").GetBoolean());
        Assert.True(detailJson.GetProperty("hasBack").GetBoolean());
        Assert.Equal((string)completed["number"]!, detailJson.GetProperty("number").GetString());
        Assert.InRange(new DateTimeOffset((DateTime)completed["manufacturedAtUtc"]!), before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(f.Actor, completed["photoRegisteredBy"]);
        Assert.Equal(1m, await f.Balance("Finished", family));
        Assert.Equal(-2m, await f.Balance("Material", material));
        using var replay = await Photo("back", bytes);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode); // completed photos cannot be replaced, even with a reason
        Assert.Equal(1m, await f.Balance("Finished", family));
        using var correctionForm = new MultipartFormDataContent();
        correctionForm.Add(new ByteArrayContent(bytes), "file", "correction.png");
        correctionForm.Add(new StringContent("관리자 정정 사유"), "reason");
        using var correction = await client.PutAsync($"/api/interior-busbar/products/{product}/photos/front",correctionForm,TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest,correction.StatusCode);
        Assert.Contains("교체하거나 삭제할 수 없습니다",(await correction.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken)).GetProperty("message").GetString());
        Assert.Equal(2L,await f.Scalar("select count(*) from busbar_photo_history"));
        using var qr = await client.GetAsync($"/api/interior-busbar/products/{product}/qr", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, qr.StatusCode);
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task ReviewSafeQrGetDoesNotBackfillPublishedProduct_AndCanReadExistingArtifact()
    {
        var options = new InteriorBusbarPublicationOptions(false,new Uri("https://busbar.example.test/"),null,"");
        await using var f = await InteriorBusbarStoreTests.Fixture.Create(options);
        var family = await f.Store.Master("product-families",new(null,"F","Family"),f.Actor);
        var material = await f.Store.Master("materials",new(null,"M","Material","개","도급"),f.Actor);
        var worker = await f.Store.Master("workers",new(null,"W","Worker"),f.Actor);
        await f.Store.Bom(new(family,[new(material,1)]),f.Actor);
        var product = await f.Store.Product(new(Guid.NewGuid(),family,worker),f.Actor);
        await f.Store.Photo(product,"front",[1],null,f.Actor);
        await f.Store.Photo(product,"back",[2],null,f.Actor);
        await using (var c = new Npgsql.NpgsqlConnection(f.Connection))
        {
            await c.OpenAsync(TestContext.Current.CancellationToken);
            await using var cmd = new Npgsql.NpgsqlCommand("update busbar_products set publication_state='Published',published_revision=revision;delete from busbar_product_qr",c);
            await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        using var factory = QmsWebApplicationFactory.Create("Development", new Dictionary<string,string?>
        {
            ["ReviewSafe:Enabled"]="true",["DevAuthentication:Enabled"]="true",
            ["Database:ApplyMigrationsOnStartup"]="false",["DevelopmentData:SeedEnabled"]="false",
            ["ConnectionStrings:QmsDatabase"]=f.Connection,
            ["InteriorBusbar:Publication:Enabled"]="false",
            ["InteriorBusbar:Publication:PublicBaseUrl"]="https://busbar.example.test/"
        },identityStore:new MutableIdentity(f.Actor));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader,"busbar-fixture");
        var blocked = await client.GetAsync($"/api/interior-busbar/products/{product}/qr",TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict,blocked.StatusCode);
        Assert.Contains("qr_generation_pending",await blocked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0L,await f.Scalar("select count(*) from busbar_product_qr"));
        var persisted = await f.Store.GetPrintableQr(product);
        var available = await client.GetAsync($"/api/interior-busbar/products/{product}/qr",TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK,available.StatusCode);
        Assert.Equal(persisted,await available.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1L,await f.Scalar("select count(*) from busbar_product_qr"));
    }

    private sealed class OsanAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var target = new BusinessUnitDatabaseTarget(BusinessUnitCodes.Osan, BusinessUnitDatabaseKind.Business,
                "NeverConnect", "NeverConnect", "NeverConnect", "NeverConnect", "", "", "", false, false, false);
            BusinessUnitRequestContextFeature.Set(Context, new(BusinessUnitAccessStatuses.Selected, Guid.NewGuid(), target, [BusinessUnitCodes.Osan], false, "test"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new(QmsClaimTypes.UserId, Guid.NewGuid().ToString()),
                new(QmsClaimTypes.BusinessUnitAccessStatus, BusinessUnitAccessStatuses.Selected),
                new(ClaimTypes.Role, QmsRoles.SystemAdministrator)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class FixedContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    [Theory(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    [InlineData("sales")]
    [InlineData("production-planning")]
    [InlineData("manufacturing")]
    [InlineData("quality")]
    public async Task DepartmentPermissionsAreEnforcedOnActualMutationRoutes(string department)
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var identity = new MutableIdentity(f.Actor) { Manager = false, DepartmentCode = department };
        using var factory = QmsWebApplicationFactory.Create("Testing", new Dictionary<string,string?> {
            ["DevAuthentication:Enabled"]="true", ["Database:ApplyMigrationsOnStartup"]="false",
            ["ConnectionStrings:QmsDatabase"]=f.Connection, ["InteriorBusbar:Publication:Enabled"]="false"
        }, identityStore: identity);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "busbar-fixture");
        var family = await f.Store.Master("product-families", new(null,"F","Family"), f.Actor);
        var material = await f.Store.Master("materials", new(null,"M","Material","m","도급"), f.Actor);
        await f.Store.Settings(new("SYN"), f.Actor);
        var project = await f.Store.Project(new(null,"Seed","WO",family,10,"Destination",new(2026,10,1)),f.Actor);
        var purchase = await f.Store.Purchase(new(null,"SeedPO",material,10,new(2026,10,1)),f.Actor);
        await f.Store.Adjustment(new(Guid.NewGuid(),"Finished",family,10,"Seed",true),f.Actor);
        var mutations = new (string Path, object Body, bool Allowed)[] {
            ("/projects", new BusbarProjectRequest(null,"Project","SYN",family,1,"Destination",new(2026,10,1)), department=="sales"),
            ("/plans", new BusbarPlanRequest(null,family,new(2026,10,1),1), department=="production-planning"),
            ("/purchases", new BusbarPurchaseRequest(null,"PO",material,1,new(2026,10,1)), department=="production-planning"),
            ("/shipments", await f.ShipmentRequest(project,1), department=="sales"),
            ("/receipts", new BusbarReceiptRequest(Guid.NewGuid(),purchase,1), department=="production-planning"),
            ("/workers", new BusbarMasterRequest(null,"W","Worker"), false),
            ("/adjustments", new BusbarAdjustmentRequest(Guid.NewGuid(),"Finished",family,1,"Opening",true), false),
            ("/ecount/resume", new BusbarEcountRetryRequest("Unauthorized"), false),
            ($"/ledger/{Guid.NewGuid()}/reverse", new BusbarReverseRequest(Guid.NewGuid(),"Unauthorized"), false)
        };
        foreach (var (path,body,allowed) in mutations)
        {
            using var response = await client.PostAsJsonAsync("/api/interior-busbar"+path, body, TestContext.Current.CancellationToken);
            Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var invalidPhotoForm = new MultipartFormDataContent();
        invalidPhotoForm.Add(new ByteArrayContent([1,2,3]), "file", "invalid.png");
        using var photo = await client.PutAsync($"/api/interior-busbar/products/{Guid.NewGuid()}/photos/front", invalidPhotoForm, TestContext.Current.CancellationToken);
        // A manufacturer reaches image validation; all other teams are rejected before it.
        Assert.Equal(department=="manufacturing" ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, photo.StatusCode);
        using var workspace = await client.GetAsync("/api/interior-busbar/workspace", TestContext.Current.CancellationToken);
        var data = System.Text.Json.JsonDocument.Parse(await workspace.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal(department=="sales", data.GetProperty("permissions").GetProperty("projects").GetBoolean());
        Assert.False(data.GetProperty("permissions").GetProperty("administration").GetBoolean());
        using var projectDetail = await client.GetAsync($"/api/interior-busbar/projects/{project}",TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK,projectDetail.StatusCode);
        var trace = await projectDetail.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(department=="sales" ? 1 : 0,trace.GetProperty("panels").GetArrayLength());
        Assert.Equal(department=="sales" ? 9 : 10,trace.GetProperty("project").GetProperty("remainingQuantity").GetInt32());
        using var scanMissing = await client.GetAsync($"/api/interior-busbar/projects/{project}/scan?code=unregistered",TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest,scanMissing.StatusCode);
        identity.DepartmentCode="quality";
        using var afterTransfer = await client.PostAsJsonAsync("/api/interior-busbar/plans", new BusbarPlanRequest(null,family,new(2026,10,2),1), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, afterTransfer.StatusCode);
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task MasterGrantsAreSeparateFromAdministrationAndRevokedOnNextRequest()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var identity = new MutableIdentity(f.Actor) { BusbarOnly = true };
        using var factory = QmsWebApplicationFactory.Create("Testing", new Dictionary<string,string?> {
            ["DevAuthentication:Enabled"]="true",["Database:ApplyMigrationsOnStartup"]="false",["ConnectionStrings:QmsDatabase"]=f.Connection
        },identityStore:identity);
        using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader,"busbar-fixture");
        async Task<HttpStatusCode> Read(string path) => (await client.GetAsync("/api/interior-busbar"+path,TestContext.Current.CancellationToken)).StatusCode;
        async Task<HttpStatusCode> WriteWorker() => (await client.PostAsJsonAsync("/api/interior-busbar/workers",new BusbarMasterRequest(null,Guid.NewGuid().ToString(),"Synthetic"),TestContext.Current.CancellationToken)).StatusCode;
        Assert.Equal(HttpStatusCode.Forbidden,await Read("/masters"));
        Assert.Equal(HttpStatusCode.Forbidden,await WriteWorker());
        Assert.Equal(HttpStatusCode.Forbidden,await Read("/master-access"));
        var denied=await client.PutAsJsonAsync("/api/interior-busbar/master-access",new BusbarMasterAccessRequest(f.Actor,"Edit","Self grant denied"),TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        // System administrator can assign even without a grant; ordinary users cannot grant themselves.
        identity.BusbarOnly=false;
        Assert.Equal(HttpStatusCode.OK,await Read("/masters"));
        var listed = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/interior-busbar/master-access",TestContext.Current.CancellationToken);
        Assert.Equal(f.Actor,listed[0].GetProperty("userId").GetGuid());
        Assert.True(listed[0].GetProperty("automatic").GetBoolean());
        var granted=await client.PutAsJsonAsync("/api/interior-busbar/master-access",new BusbarMasterAccessRequest(f.Actor,"Read","Synthetic read grant"),TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK,granted.StatusCode);
        identity.Manager=false;
        Assert.Equal(HttpStatusCode.OK,await Read("/masters"));
        Assert.Equal(HttpStatusCode.Forbidden,await WriteWorker());
        await f.Store.SetMasterAccess(new(f.Actor,"Edit","Synthetic edit grant"),f.Actor);
        Assert.Equal(HttpStatusCode.OK,await WriteWorker());
        var adjustment=await client.PostAsJsonAsync("/api/interior-busbar/adjustments",new BusbarAdjustmentRequest(Guid.NewGuid(),"Finished",Guid.NewGuid(),1,"Not delegated",true),TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden,adjustment.StatusCode);
        await f.Store.SetMasterAccess(new(f.Actor,"None","Synthetic revoke"),f.Actor);
        Assert.Equal(HttpStatusCode.Forbidden,await Read("/masters"));
        Assert.Equal(HttpStatusCode.Forbidden,await WriteWorker());
        await f.Store.SetMasterAccess(new(f.Actor,"Edit","Synthetic grant"),f.Actor);
        identity.Active=false;
        Assert.Equal(HttpStatusCode.Unauthorized,await Read("/masters"));
        Assert.Equal(4,await f.Scalar("select count(*) from busbar_audit where entity_kind='MasterAccess'"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task DeleteRestoreRoutesEnforceOwningPermissionAndHonorDynamicMasterGrant()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        await f.Store.Settings(new("SYN"), f.Actor);
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        var material = await f.Store.Master("materials", new(null, "M", "Material", "개", "도급"), f.Actor);
        var worker = await f.Store.Master("workers", new(null, "W", "Worker"), f.Actor);
        var project = await f.Store.Project(new(null, "Project", "WO", family, 1, "Destination", new(2026, 10, 1)), f.Actor);
        var plan = await f.Store.Plan(new(Guid.NewGuid(), family, new(2026, 10, 1), 0), f.Actor);
        var purchase = await f.Store.Purchase(new(null, "PO", material, 1, new(2026, 10, 1)), f.Actor);
        var identity = new MutableIdentity(f.Actor) { Manager = false, DepartmentCode = "quality" };
        await f.Store.SetMasterAccess(new(f.Actor, "Edit", "Synthetic grant"), f.Actor);
        using var factory = QmsWebApplicationFactory.Create("Testing", new Dictionary<string, string?> {
            ["DevAuthentication:Enabled"]="true", ["Database:ApplyMigrationsOnStartup"]="false",
            ["ConnectionStrings:QmsDatabase"]=f.Connection, ["InteriorBusbar:Publication:Enabled"]="false"
        }, identityStore: identity);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "busbar-fixture");
        async Task<HttpStatusCode> Mutate(string path, string reason)
        {
            using var response = await client.PostAsJsonAsync("/api/interior-busbar" + path, new BusbarDeleteRequest(reason), TestContext.Current.CancellationToken);
            return response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, await Mutate($"/workers/{worker}/delete", "미사용 삭제"));
        Assert.Equal(HttpStatusCode.OK, await Mutate($"/workers/{worker}/restore", "복원"));
        Assert.Equal(HttpStatusCode.Forbidden, await Mutate($"/projects/{project}/delete", "권한 없음"));
        identity.DepartmentCode = "sales";
        Assert.Equal(HttpStatusCode.OK, await Mutate($"/projects/{project}/delete", "영업 삭제"));
        Assert.Equal(HttpStatusCode.Forbidden, await Mutate($"/plans/{plan}/delete", "권한 없음"));
        identity.DepartmentCode = "production-planning";
        Assert.Equal(HttpStatusCode.OK, await Mutate($"/plans/{plan}/delete", "계획 삭제"));
        Assert.Equal(HttpStatusCode.OK, await Mutate($"/purchases/{purchase}/delete", "발주 삭제"));
        Assert.Equal(HttpStatusCode.Forbidden, await Mutate($"/projects/{project}/restore", "권한 없음"));
    }

    private sealed class MutableIdentity(Guid id) : IIdentityStore
    {
        public bool Manager { get; set; } = true;
        public bool BusbarOnly { get; set; }
        public bool Active { get; set; } = true;
        public string DepartmentCode { get; set; } = "manufacturing";
        private UserAuthorizationProfile Profile => new(new QmsUser(id, "busbar-fixture", "Synthetic Manager", DepartmentCode, Active),
            SeedIdentityData.Departments.Single(d => d.Code == DepartmentCode),
            Manager ? [new Role(Guid.NewGuid(), BusbarOnly ? InteriorBusbarEndpointExtensions.ManagerRole : QmsRoles.SystemAdministrator, "Synthetic administrator")] : [new Role(Guid.NewGuid(), QmsRoles.ReadOnly, "Read Only")], [], []);
        public Task<UserAuthorizationProfile?> GetProfileByDevelopmentUserKeyAsync(string key, CancellationToken token) => Task.FromResult<UserAuthorizationProfile?>(key == "busbar-fixture" ? Profile : null);
        public Task<UserAuthorizationProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken token) => Task.FromResult<UserAuthorizationProfile?>(userId == id ? Profile : null);
        public Task<QmsProject?> GetProjectByKeyAsync(string key, CancellationToken token) => Task.FromResult<QmsProject?>(null);
        public Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<UserSummary>>([new("busbar-fixture","Synthetic Manager",DepartmentCode,Profile.Roles.Select(r=>r.Code).ToArray(),id,IsActive:Active)]);
    }
}
