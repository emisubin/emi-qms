using System.Net;
using System.Text.Json;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class ProjectRegistrationApiTests
{
    [Fact]
    public async Task RuntimePools_ConcurrentDetailAndSummaryReadsKeepTheirOwnResults()
    {
        await using var context = await ProjectApiTestContext.CreateAsync(useSharedRuntimePool: true);
        using var client = context.CreateClient("dev-admin");
        var projectId = await CreateProjectAndReadIdAsync(client, "POOL-READS", "Pool reader isolation", 2);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var routes = new[]
        {
            ($"/api/projects/{projectId}", "projectId"),
            ($"/api/pending?projectId={projectId}", "items"),
            ("/api/my-work/summary", "requestedCount"),
            ("/api/admin/dashboard", "failedDeliveryCount"),
            ("/api/notifications/summary", "unreadCount"),
            ("/api/pending-types/filter-options", "")
        };

        // These are real HTTP/store/reader lifetimes sharing a deliberately small
        // pool, including detail/summary fan-out and independent neighboring reads.
        for (var round = 0; round < 24; round++)
        {
            await Task.WhenAll(routes.Select(async route =>
            {
                using var response = await client.GetAsync(route.Item1, deadline.Token);
                Assert.True(response.StatusCode == HttpStatusCode.OK,
                    $"Round {round}, {route.Item1}: {response.StatusCode}. {context.ErrorLogs()}");
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
                if (route.Item2.Length == 0)
                {
                    Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
                    return;
                }
                Assert.True(body.RootElement.TryGetProperty(route.Item2, out var value), route.Item1);
                if (route.Item2 == "projectId") Assert.Equal(projectId, value.GetGuid());
            }));
        }
    }
}
