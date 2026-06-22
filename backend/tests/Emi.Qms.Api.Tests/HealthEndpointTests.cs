using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task LiveHealth_IsAnonymousAndReturnsOk()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"name\":\"live\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"status\":\"ok\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadyHealth_ReturnsHealthPayloadEvenWhenDatabaseIsUnavailable()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"name\":\"ready\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"database\"", body, StringComparison.OrdinalIgnoreCase);
    }
}
