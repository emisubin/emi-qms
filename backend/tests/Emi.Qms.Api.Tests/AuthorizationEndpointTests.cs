using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class AuthorizationEndpointTests(QmsWebApplicationFactory factory)
    : IClassFixture<QmsWebApplicationFactory>
{
    [Fact]
    public async Task CorsPolicy_PrefersFrontendOriginEnvironmentOverride()
    {
        const string expectedOrigin = "http://127.0.0.1:5174";
        using var corsFactory = QmsWebApplicationFactory.Create(
            "Development",
            new Dictionary<string, string?>
            {
                ["Frontend:Origin"] = "http://localhost:5173",
                ["FRONTEND_ORIGIN"] = expectedOrigin,
                ["DevAuthentication:Enabled"] = "true",
                ["DevelopmentData:SeedEnabled"] = "false",
                ["Database:ApplyMigrationsOnStartup"] = "false"
            },
            includeDefaultDevelopmentAuthentication: true);
        using var client = corsFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/projects");
        request.Headers.Add("Origin", expectedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "x-dev-user");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains(expectedOrigin, origins);
    }

    [Fact]
    public async Task ProtectedApi_ReturnsUnauthorized_WhenRequestIsAnonymous()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProjectApi_ReturnsForbidden_WhenAuthenticatedUserHasNoRole()
    {
        using var client = CreateClientFor("dev-no-role");

        var response = await client.GetAsync(
            "/api/projects/demo-project-alpha/overview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("dev-admin")]
    [InlineData("dev-sales")]
    [InlineData("dev-design")]
    [InlineData("dev-production")]
    [InlineData("dev-manufacturing")]
    [InlineData("dev-quality")]
    [InlineData("dev-logistics")]
    [InlineData("dev-viewer")]
    public async Task ProjectApi_ReturnsSuccess_ForEveryActiveInternalRoleAcrossAllProjects(
        string developmentUserKey)
    {
        using var client = CreateClientFor(developmentUserKey);

        var alphaResponse = await client.GetAsync(
            "/api/projects/demo-project-alpha/overview",
            TestContext.Current.CancellationToken);
        var betaResponse = await client.GetAsync(
            "/api/projects/demo-project-beta/overview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, alphaResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, betaResponse.StatusCode);
    }

    [Fact]
    public async Task ProjectApi_UsesUserProjectAccess_WhenAccountDoesNotHaveProjectReadAll()
    {
        using var limitedFactory = QmsWebApplicationFactory.Create(
            "Testing",
            includeDefaultDevelopmentAuthentication: true,
            identityStore: new LimitedProjectIdentityStore());
        using var client = CreateClientFor(limitedFactory, "dev-limited");

        var alphaResponse = await client.GetAsync(
            "/api/projects/demo-project-alpha/overview",
            TestContext.Current.CancellationToken);
        var betaResponse = await client.GetAsync(
            "/api/projects/demo-project-beta/overview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, alphaResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, betaResponse.StatusCode);

        var project = await alphaResponse.Content.ReadFromJsonAsync<ProjectOverviewDto>(
            TestContext.Current.CancellationToken);
        Assert.Equal("demo-project-alpha", project?.ProjectKey);
    }

    [Fact]
    public async Task ProjectApi_DoesNotTreatLegacyProjectAccessAllAsFullProjectRead()
    {
        using var limitedFactory = QmsWebApplicationFactory.Create(
            "Testing",
            includeDefaultDevelopmentAuthentication: true,
            identityStore: new LimitedProjectIdentityStore());
        using var client = CreateClientFor(limitedFactory, "dev-legacy-project-access-all");

        var assignedResponse = await client.GetAsync(
            "/api/projects/demo-project-alpha/overview",
            TestContext.Current.CancellationToken);
        var unassignedResponse = await client.GetAsync(
            "/api/projects/demo-project-beta/overview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, assignedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unassignedResponse.StatusCode);
    }

    [Fact]
    public async Task ProjectApi_ReturnsForbidden_WhenProjectReadUserHasNoProjectReadAllAndNoUserProjectAccess()
    {
        using var limitedFactory = QmsWebApplicationFactory.Create(
            "Testing",
            includeDefaultDevelopmentAuthentication: true,
            identityStore: new LimitedProjectIdentityStore());
        using var client = CreateClientFor(limitedFactory, "dev-unassigned-project-reader");

        var response = await client.GetAsync(
            "/api/projects/demo-project-alpha/overview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProjectApi_ReturnsUnauthorized_WhenDevelopmentUserDoesNotExist()
    {
        using var client = CreateClientFor("dev-missing-user");

        var response = await client.GetAsync(
            "/api/projects/demo-project-alpha/overview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProjectApi_ReturnsUnauthorized_WhenDevelopmentUserIsInactive()
    {
        using var client = CreateClientFor("dev-disabled");

        var response = await client.GetAsync(
            "/api/projects/demo-project-alpha/overview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProjectApi_ReturnsUnauthorized_WhenInactiveUserHasRoleInformation()
    {
        using var client = CreateClientFor("dev-disabled");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiBlocksDirectAdminAccess_EvenIfClientMenuWereVisible()
    {
        using var client = CreateClientFor("dev-logistics");

        var response = await client.GetAsync("/api/admin/users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void ApplicationStartupFails_WhenDevelopmentAuthenticationIsEnabledInProduction()
    {
        using var productionFactory = QmsWebApplicationFactory.Create(
            "Production",
            new Dictionary<string, string?> { ["DevAuthentication:Enabled"] = "true" });

        var exception = Assert.Throws<InvalidOperationException>(() => productionFactory.CreateClient());
        Assert.Contains("development authentication cannot be enabled", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminPermission_IsDistinctFromGeneralUserPermission()
    {
        using var adminClient = CreateClientFor("dev-admin");
        using var salesClient = CreateClientFor("dev-sales");

        var adminResponse = await adminClient.GetAsync("/api/admin/users", TestContext.Current.CancellationToken);
        var salesResponse = await salesClient.GetAsync("/api/admin/users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, salesResponse.StatusCode);
    }

    [Fact]
    public async Task ManufacturingUser_CanReadProjectsButCannotManageProjects()
    {
        using var client = CreateClientFor("dev-manufacturing");

        var readResponse = await client.GetAsync(
            "/api/projects/demo-project-beta/overview",
            TestContext.Current.CancellationToken);
        var manageResult = await AuthorizeSeedUserAsync("dev-manufacturing", QmsPolicies.ProjectManage);

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.False(manageResult.Succeeded);
    }

    [Fact]
    public async Task QualityUser_CanReadManufacturingStatusButCannotUpdateManufacturingRecords()
    {
        using var client = CreateClientFor("dev-quality");

        var readResponse = await client.GetAsync(
            "/api/projects/demo-project-alpha/overview",
            TestContext.Current.CancellationToken);
        var updateResult = await AuthorizeSeedUserAsync("dev-quality", QmsPolicies.ManufacturingUpdate);

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.False(updateResult.Succeeded);
    }

    [Fact]
    public async Task SalesUser_HasProjectWritePermission()
    {
        var manageResult = await AuthorizeSeedUserAsync("dev-sales", QmsPolicies.ProjectManage);

        Assert.True(manageResult.Succeeded);
    }

    [Theory]
    [InlineData("dev-admin", true)]
    [InlineData("dev-sales", true)]
    [InlineData("dev-design", false)]
    [InlineData("dev-production", false)]
    [InlineData("dev-manufacturing", false)]
    [InlineData("dev-quality", false)]
    [InlineData("dev-logistics", false)]
    [InlineData("dev-viewer", false)]
    public async Task ProjectSalesAmountReadPolicy_AllowsOnlySalesAndAdministrator(
        string developmentUserKey,
        bool expectedAllowed)
    {
        var result = await AuthorizeSeedUserAsync(
            developmentUserKey,
            QmsPolicies.ProjectSalesAmountRead);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Theory]
    [InlineData("dev-admin", true)]
    [InlineData("dev-sales", true)]
    [InlineData("dev-design", false)]
    [InlineData("dev-production", false)]
    [InlineData("dev-manufacturing", false)]
    [InlineData("dev-quality", false)]
    [InlineData("dev-logistics", false)]
    [InlineData("dev-viewer", false)]
    public async Task ManufacturingWorkTimeReadPolicy_AllowsOnlySalesAndAdministrator(
        string developmentUserKey,
        bool expectedAllowed)
    {
        var result = await AuthorizeSeedUserAsync(
            developmentUserKey,
            QmsPolicies.ManufacturingWorkTimeRead);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Theory]
    [InlineData("dev-design", true)]
    [InlineData("dev-sales", true)]
    [InlineData("dev-production", true)]
    [InlineData("dev-admin", false)]
    [InlineData("dev-manufacturing", false)]
    [InlineData("dev-quality", false)]
    [InlineData("dev-logistics", false)]
    [InlineData("dev-viewer", false)]
    public async Task PanelInfoUpdatePolicy_AllowsDesignSalesAndProductionPlanningOnly(
        string developmentUserKey,
        bool expectedAllowed)
    {
        var result = await AuthorizeSeedUserAsync(
            developmentUserKey,
            QmsPolicies.PanelInfoUpdate);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    private HttpClient CreateClientFor(string developmentUserKey)
    {
        return CreateClientFor(factory, developmentUserKey);
    }

    private static HttpClient CreateClientFor(QmsWebApplicationFactory webApplicationFactory, string developmentUserKey)
    {
        var client = webApplicationFactory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUserKey);
        return client;
    }

    private async Task<AuthorizationResult> AuthorizeSeedUserAsync(
        string developmentUserKey,
        string policyName)
    {
        var principal = await CreatePrincipalAsync(developmentUserKey);
        var authorizationService = factory.Services.GetRequiredService<IAuthorizationService>();

        return await authorizationService.AuthorizeAsync(principal, resource: null, policyName);
    }

    private static async Task<ClaimsPrincipal> CreatePrincipalAsync(string developmentUserKey)
    {
        var store = new InMemoryIdentityStore();
        var profile = await store.GetProfileByDevelopmentUserKeyAsync(
            developmentUserKey,
            TestContext.Current.CancellationToken);
        Assert.NotNull(profile);

        var claims = new List<Claim>
        {
            new(QmsClaimTypes.UserId, profile.User.Id.ToString("D")),
            new(QmsClaimTypes.DevelopmentUserKey, profile.User.DevelopmentUserKey),
            new(ClaimTypes.NameIdentifier, profile.User.Id.ToString("D")),
            new(ClaimTypes.Name, profile.User.DevelopmentUserKey)
        };

        claims.AddRange(profile.Roles.Select(role => new Claim(ClaimTypes.Role, role.Code)));
        claims.AddRange(profile.Permissions.Select(permission => new Claim(QmsClaimTypes.Permission, permission.Code)));
        claims.AddRange(profile.ProjectAccess.Select(project => new Claim(QmsClaimTypes.Project, project.ProjectKey)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, DevelopmentAuthenticationDefaults.Scheme));
    }

    private sealed record ProjectOverviewDto(string ProjectKey, string ProjectNumber, string Name, string Status);

    private sealed class LimitedProjectIdentityStore : IIdentityStore
    {
        private static readonly Department Department = new(
            new Guid("60000000-0000-0000-0000-000000000001"),
            "limited",
            "Limited");

        private static readonly QmsUser LimitedUser = new(
            new Guid("60000000-0000-0000-0000-000000000002"),
            "dev-limited",
            "Dev Limited Project User",
            Department.Code,
            true);

        private static readonly QmsUser LegacyAccessUser = new(
            new Guid("60000000-0000-0000-0000-000000000006"),
            "dev-legacy-project-access-all",
            "Dev Legacy Project Access User",
            Department.Code,
            true);

        private static readonly QmsUser UnassignedProjectReader = new(
            new Guid("60000000-0000-0000-0000-000000000007"),
            "dev-unassigned-project-reader",
            "Dev Unassigned Project Reader",
            Department.Code,
            true);

        private static readonly Permission ProjectRead = new(
            new Guid("60000000-0000-0000-0000-000000000003"),
            QmsPermissions.ProjectRead,
            "Read projects");

        private static readonly Permission LegacyProjectAccessAll = new(
            new Guid("60000000-0000-0000-0000-000000000008"),
            QmsPermissions.ProjectAccessAll,
            "Legacy access every project");

        private static readonly QmsProject Alpha = new(
            new Guid("60000000-0000-0000-0000-000000000004"),
            "demo-project-alpha",
            "DEMO-24001",
            "Demo Project Alpha");

        private static readonly QmsProject Beta = new(
            new Guid("60000000-0000-0000-0000-000000000005"),
            "demo-project-beta",
            "DEMO-24002",
            "Demo Project Beta");

        public Task<UserAuthorizationProfile?> GetProfileByDevelopmentUserKeyAsync(
            string developmentUserKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<UserAuthorizationProfile?>(developmentUserKey switch
            {
                "dev-limited" => BuildProfile(LimitedUser, [ProjectRead], [Alpha]),
                "dev-legacy-project-access-all" => BuildProfile(
                    LegacyAccessUser,
                    [ProjectRead, LegacyProjectAccessAll],
                    [Alpha]),
                "dev-unassigned-project-reader" => BuildProfile(UnassignedProjectReader, [ProjectRead], []),
                _ => null
            });
        }

        public Task<UserAuthorizationProfile?> GetProfileByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<UserAuthorizationProfile?>(userId switch
            {
                var id when id == LimitedUser.Id => BuildProfile(LimitedUser, [ProjectRead], [Alpha]),
                var id when id == LegacyAccessUser.Id => BuildProfile(
                    LegacyAccessUser,
                    [ProjectRead, LegacyProjectAccessAll],
                    [Alpha]),
                var id when id == UnassignedProjectReader.Id => BuildProfile(UnassignedProjectReader, [ProjectRead], []),
                _ => null
            });
        }

        public Task<QmsProject?> GetProjectByKeyAsync(string projectKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<QmsProject?>(projectKey switch
            {
                "demo-project-alpha" => Alpha,
                "demo-project-beta" => Beta,
                _ => null
            });
        }

        public Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<UserSummary> users =
            [
                new(LimitedUser.DevelopmentUserKey, LimitedUser.DisplayName, LimitedUser.DepartmentCode, []),
                new(LegacyAccessUser.DevelopmentUserKey, LegacyAccessUser.DisplayName, LegacyAccessUser.DepartmentCode, []),
                new(
                    UnassignedProjectReader.DevelopmentUserKey,
                    UnassignedProjectReader.DisplayName,
                    UnassignedProjectReader.DepartmentCode,
                    [])
            ];

            return Task.FromResult(users);
        }

        private static UserAuthorizationProfile BuildProfile(
            QmsUser user,
            IReadOnlyList<Permission> permissions,
            IReadOnlyList<QmsProject> projectAccess)
        {
            return new UserAuthorizationProfile(user, Department, [], permissions, projectAccess);
        }
    }
}
