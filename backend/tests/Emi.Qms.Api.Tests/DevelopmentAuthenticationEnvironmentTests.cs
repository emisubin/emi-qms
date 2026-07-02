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
        using var factory = CreateFactory(environment, "true", authMode: "Dev");
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
        using var factory = CreateFactory("Development", "false", authMode: "Dev");
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentAuthentication_IsDisabledWhenSettingIsInvalid()
    {
        using var factory = CreateFactory("Development", "not-a-bool", authMode: "Dev");
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentAuthentication_WorksWhenEnvironmentVariableStyleSettingIsTrue()
    {
        using var factory = QmsWebApplicationFactory.Create(
            "Testing",
            new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = "Dev",
                ["DEV_AUTHENTICATION_ENABLED"] = "true"
            });
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BearerHeaderTakesPrecedenceOverDevelopmentUserHeader()
    {
        using var factory = CreateFactory("Testing", "true", authMode: "Dev");
        using var client = CreateClientFor(factory, "dev-admin");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer invalid-token");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.EventId.Id == 2001);
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
        using var factory = CreateFactory(environment, "false", includeAzureAdConfiguration: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HeaderCannotAuthenticateUser_WhenEnvironmentIsNotAllowed()
    {
        using var factory = CreateFactory("Staging", "false", includeAzureAdConfiguration: true);
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentUserHeaderIsIgnoredInEntraMode()
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = "EntraId",
            ["DevAuthentication:Enabled"] = "true"
        };
        AddAzureAdConfiguration(configuration);
        using var factory = QmsWebApplicationFactory.Create("Testing", configuration);
        using var client = CreateClientFor(factory, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ApplicationStartupFails_WhenEntraModeConfigurationIsMissing(string environment)
    {
        using var factory = CreateFactory(environment, "false");

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("EntraId authentication requires", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationStartupFails_WhenEntraModeIsExplicitAndConfigurationIsMissingInTesting()
    {
        using var factory = QmsWebApplicationFactory.Create(
            "Testing",
            new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = "EntraId",
                ["DevAuthentication:Enabled"] = "true"
            });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("EntraId authentication requires", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ApplicationStartupFails_WhenDevModeIsExplicitOutsideAllowedEnvironments(string environment)
    {
        using var factory = QmsWebApplicationFactory.Create(
            environment,
            new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = "Dev",
                ["DevAuthentication:Enabled"] = "false"
            });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Dev authentication mode cannot be used", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ApplicationStartupFails_WhenAdminUserSwitchIsEnabledOutsideAllowedEnvironments(string environment)
    {
        using var factory = QmsWebApplicationFactory.Create(
            environment,
            new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = "EntraId",
                ["AdminUserSwitch:Enabled"] = "true"
            });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("admin user switch cannot be enabled", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("UAT")]
    public async Task ApplicationStarts_WhenAdminUserSwitchIsEnabledInAllowedEnvironments(string environment)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = "EntraId",
            ["AdminUserSwitch:Enabled"] = "true"
        };
        AddAzureAdConfiguration(configuration);
        using var factory = QmsWebApplicationFactory.Create(environment, configuration);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static QmsWebApplicationFactory CreateFactory(
        string environment,
        string enabled,
        bool includeAzureAdConfiguration = false,
        string? authMode = null)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["DevAuthentication:Enabled"] = enabled
        };
        if (!string.IsNullOrWhiteSpace(authMode))
        {
            configuration["Authentication:Mode"] = authMode;
        }

        if (includeAzureAdConfiguration)
        {
            AddAzureAdConfiguration(configuration);
        }

        return QmsWebApplicationFactory.Create(
            environment,
            configuration);
    }

    private static void AddAzureAdConfiguration(Dictionary<string, string?> configuration)
    {
        configuration["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111";
        configuration["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222";
        configuration["AzureAd:Instance"] = "https://login.microsoftonline.com/";
        configuration["AzureAd:Audience"] = "api://22222222-2222-2222-2222-222222222222";
    }

    private static HttpClient CreateClientFor(QmsWebApplicationFactory factory, string developmentUserKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUserKey);
        return client;
    }
}
