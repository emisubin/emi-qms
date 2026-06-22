using System.Net;
using Emi.Qms.Api.Authorization;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class DevelopmentAuthenticationEnvironmentTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task DevelopmentAuthentication_WorksOnlyInAllowedEnvironmentWithExplicitTrue(string environment)
    {
        using var factory = CreateFactory(environment, "true");
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task DevelopmentAuthentication_IsDisabledInAllowedEnvironmentWithoutExplicitSetting(string environment)
    {
        using var factory = QmsWebApplicationFactory.Create(environment);
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentAuthentication_IsDisabledInDevelopmentWhenExplicitFalse()
    {
        using var factory = CreateFactory("Development", "false");
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentAuthentication_IsDisabledWhenSettingIsInvalid()
    {
        using var factory = CreateFactory("Development", "not-a-bool");
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentAuthentication_WorksWhenEnvironmentVariableStyleSettingIsTrue()
    {
        using var factory = QmsWebApplicationFactory.Create(
            "Testing",
            new Dictionary<string, string?> { ["DEV_AUTHENTICATION_ENABLED"] = "true" });
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    [InlineData("Sandbox")]
    public void ApplicationStartupFails_WhenDevelopmentAuthenticationTrueOutsideAllowedEnvironments(string environment)
    {
        using var factory = CreateFactory(environment, "true");

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("development authentication cannot be enabled", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task ApplicationStarts_WhenDevelopmentAuthenticationFalseOutsideAllowedEnvironments(string environment)
    {
        using var factory = CreateFactory(environment, "false");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HeaderCannotAuthenticateUser_WhenEnvironmentIsNotAllowed()
    {
        using var factory = CreateFactory("Staging", "false");
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static QmsWebApplicationFactory CreateFactory(string environment, string enabled)
    {
        return QmsWebApplicationFactory.Create(
            environment,
            new Dictionary<string, string?> { ["DevAuthentication:Enabled"] = enabled });
    }

    private static HttpClient CreateClientFor(QmsWebApplicationFactory factory, string developmentUserKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUserKey);
        return client;
    }
}
