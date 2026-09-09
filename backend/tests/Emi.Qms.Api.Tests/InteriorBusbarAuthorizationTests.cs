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
        var response = await client.PostAsJsonAsync("/api/interior-busbar/workers", new BusbarMasterRequest(null, "W", "Synthetic"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/interior-busbar/workspace")]
    [InlineData("GET", "/api/interior-busbar/products/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/interior-busbar/workers")]
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
        identity.Manager = false;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/interior-busbar/workspace", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/interior-busbar/workers", new BusbarMasterRequest(null, "W2", "Worker"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(1L, await fixture.Scalar("select count(*) from busbar_workers"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task RealHttpPhotoRegistrationCompletesOnceAndBlocksQrUntilPublished()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        using var factory = QmsWebApplicationFactory.Create("Testing", new Dictionary<string, string?> {
            ["DevAuthentication:Enabled"] = "true", ["Database:ApplyMigrationsOnStartup"] = "false",
            ["ConnectionStrings:QmsDatabase"] = f.Connection, ["InteriorBusbar:Publication:Enabled"] = "false"
        }, identityStore: new MutableIdentity(f.Actor));
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
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode); // completed photo replacement requires an explicit correction reason
        Assert.Equal(1m, await f.Balance("Finished", family));
        using var qr = await client.GetAsync($"/api/interior-busbar/products/{product}/qr", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, qr.StatusCode);
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

    private sealed class MutableIdentity(Guid id) : IIdentityStore
    {
        public bool Manager { get; set; } = true;
        private UserAuthorizationProfile Profile => new(new QmsUser(id, "busbar-fixture", "Synthetic Manager", "manufacturing", true),
            SeedIdentityData.Departments.Single(d => d.Code == "manufacturing"),
            Manager ? [new Role(Guid.NewGuid(), InteriorBusbarEndpointExtensions.ManagerRole, "Busbar Manager")] : [new Role(Guid.NewGuid(), QmsRoles.ReadOnly, "Read Only")], [], []);
        public Task<UserAuthorizationProfile?> GetProfileByDevelopmentUserKeyAsync(string key, CancellationToken token) => Task.FromResult<UserAuthorizationProfile?>(key == "busbar-fixture" ? Profile : null);
        public Task<UserAuthorizationProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken token) => Task.FromResult<UserAuthorizationProfile?>(userId == id ? Profile : null);
        public Task<QmsProject?> GetProjectByKeyAsync(string key, CancellationToken token) => Task.FromResult<QmsProject?>(null);
        public Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<UserSummary>>([]);
    }
}
