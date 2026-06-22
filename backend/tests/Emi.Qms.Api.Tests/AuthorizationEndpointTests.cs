using System.Net;
using System.Net.Http.Json;
using Emi.Qms.Api.Authorization;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class AuthorizationEndpointTests(QmsWebApplicationFactory factory)
    : IClassFixture<QmsWebApplicationFactory>
{
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

    [Fact]
    public async Task ProjectApi_ReturnsForbidden_WhenUserRequestsDifferentProject()
    {
        using var client = CreateClientFor("dev-viewer");

        var response = await client.GetAsync(
            "/api/projects/demo-project-beta/overview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProjectApi_ReturnsSuccess_WhenProjectAccessIsAllowed()
    {
        using var client = CreateClientFor("dev-viewer");

        var response = await client.GetAsync(
            "/api/projects/demo-project-alpha/overview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectOverviewDto>(
            TestContext.Current.CancellationToken);
        Assert.Equal("demo-project-alpha", project?.ProjectKey);
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

    private HttpClient CreateClientFor(string developmentUserKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUserKey);
        return client;
    }

    private sealed record ProjectOverviewDto(string ProjectKey, string ProjectNumber, string Name, string Status);
}
