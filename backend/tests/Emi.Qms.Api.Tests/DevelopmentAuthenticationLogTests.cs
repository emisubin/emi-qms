using System.Net;
using System.Security.Cryptography;
using System.Text;
using Emi.Qms.Api.Authorization;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class DevelopmentAuthenticationLogTests
{
    [Fact]
    public async Task UnknownDevelopmentUser_WritesSanitizedAuthenticationFailureLog()
    {
        using var factory = QmsWebApplicationFactory.Create(
            "Testing",
            new Dictionary<string, string?> { ["DevAuthentication:Enabled"] = "true" });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-missing-user");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer raw-token-value");
        client.DefaultRequestHeaders.Add("Cookie", "session=raw-cookie-value");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var log = Assert.Single(factory.Logs.Entries, entry => entry.EventId.Id == 2001);
        Assert.Contains("unknown_development_user", log.Message, StringComparison.Ordinal);
        Assert.Contains("/api/me", log.Message, StringComparison.Ordinal);
        Assert.Contains("GET", log.Message, StringComparison.Ordinal);
        Assert.Contains(Hash("dev-missing-user"), log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("dev-missing-user", log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-cookie-value", log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", log.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cookie", log.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Token", log.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InactiveDevelopmentUser_WritesSanitizedAuthenticationFailureLog()
    {
        using var factory = QmsWebApplicationFactory.Create(
            "Testing",
            new Dictionary<string, string?> { ["DevAuthentication:Enabled"] = "true" });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-disabled");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var log = Assert.Single(factory.Logs.Entries, entry => entry.EventId.Id == 2001);
        Assert.Contains("inactive_development_user", log.Message, StringComparison.Ordinal);
        Assert.Contains("/api/me", log.Message, StringComparison.Ordinal);
        Assert.Contains(Hash("dev-disabled"), log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("dev-disabled", log.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledDevelopmentAuthentication_WritesSanitizedAuthenticationFailureLog()
    {
        using var factory = QmsWebApplicationFactory.Create("Development");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var log = Assert.Single(factory.Logs.Entries, entry => entry.EventId.Id == 2001);
        Assert.Contains("development_authentication_not_enabled", log.Message, StringComparison.Ordinal);
        Assert.Contains(Hash("dev-admin"), log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("dev-admin", log.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisallowedEnvironment_WritesSanitizedAuthenticationFailureLogWhenFeatureIsDisabled()
    {
        using var factory = QmsWebApplicationFactory.Create(
            "Staging",
            new Dictionary<string, string?> { ["DevAuthentication:Enabled"] = "false" });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-admin");

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var log = Assert.Single(factory.Logs.Entries, entry => entry.EventId.Id == 2001);
        Assert.Contains("development_authentication_environment_not_allowed", log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("dev-admin", log.Message, StringComparison.Ordinal);
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
