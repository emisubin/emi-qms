using System.Net;
using System.Net.Http.Json;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class MaintenanceWorkerGateTests
{
    [Fact]
    public void ActivationPolicy_DefaultsPurgeToEnabled_AndRejectsMalformedValue()
    {
        var defaults = new ConfigurationBuilder().Build();
        var defaultActivation = MutationWorkerActivationPolicy.Evaluate(defaults, reviewSafeEnabled: false);
        Assert.False(defaultActivation.NotificationDeliveryWorkerEnabled);
        Assert.False(defaultActivation.NotificationEscalationWorkerEnabled);
        Assert.True(defaultActivation.AdminDeletionPurgeWorkerEnabled);
        Assert.True(defaultActivation.MutationWorkersEnabled);

        var disabled = Configuration(new Dictionary<string, string?>
        {
            ["AdminDeletionPurge:Enabled"] = "false"
        });
        Assert.False(MutationWorkerActivationPolicy.Evaluate(disabled, reviewSafeEnabled: false).AdminDeletionPurgeWorkerEnabled);

        var malformed = Configuration(new Dictionary<string, string?>
        {
            ["AdminDeletionPurge:Enabled"] = "not-a-boolean"
        });
        Assert.Throws<InvalidOperationException>(
            () => MutationWorkerActivationPolicy.Evaluate(malformed, reviewSafeEnabled: false));
    }

    [Fact]
    public async Task DevelopmentRegistration_UsesEffectiveWorkerOptions()
    {
        await using var defaults = Factory(new Dictionary<string, string?>
        {
            ["ReviewSafe:Enabled"] = "false",
            ["Notifications:Dispatch:Enabled"] = "false",
            ["Notifications:Escalation:Enabled"] = "false"
        });
        var defaultHostedServices = defaults.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();
        Assert.DoesNotContain(typeof(NotificationDeliveryWorker), defaultHostedServices);
        Assert.DoesNotContain(typeof(NotificationEscalationWorker), defaultHostedServices);
        Assert.Contains(typeof(AdminDeletionPurgeWorker), defaultHostedServices);

        await using var disabled = Factory(PhaseAConfiguration());
        var disabledHostedServices = disabled.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();
        Assert.DoesNotContain(typeof(NotificationDeliveryWorker), disabledHostedServices);
        Assert.DoesNotContain(typeof(NotificationEscalationWorker), disabledHostedServices);
        Assert.DoesNotContain(typeof(AdminDeletionPurgeWorker), disabledHostedServices);

        await using var enabled = Factory(new Dictionary<string, string?>
        {
            ["ReviewSafe:Enabled"] = "false",
            ["Notifications:Dispatch:Enabled"] = "true",
            ["Notifications:Escalation:Enabled"] = "true",
            ["AdminDeletionPurge:Enabled"] = "true"
        });
        var enabledHostedServices = enabled.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();
        Assert.Contains(typeof(NotificationDeliveryWorker), enabledHostedServices);
        Assert.Contains(typeof(NotificationEscalationWorker), enabledHostedServices);
        Assert.Contains(typeof(AdminDeletionPurgeWorker), enabledHostedServices);
    }

    [Fact]
    public async Task ReviewSafeRegistration_AlwaysExcludesAllMutationWorkers()
    {
        foreach (var purgeEnabled in new[] { "true", "false" })
        {
            await using var factory = Factory(new Dictionary<string, string?>
            {
                ["ReviewSafe:Enabled"] = "true",
                ["AdminDeletionPurge:Enabled"] = purgeEnabled,
                ["Notifications:Dispatch:Enabled"] = "true",
                ["Notifications:Escalation:Enabled"] = "true"
            });
            var hostedServices = factory.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();
            Assert.DoesNotContain(typeof(NotificationDeliveryWorker), hostedServices);
            Assert.DoesNotContain(typeof(NotificationEscalationWorker), hostedServices);
            Assert.DoesNotContain(typeof(AdminDeletionPurgeWorker), hostedServices);
        }
    }

    [Fact]
    public async Task DisabledWorkerDirectInvocation_DoesNotCallPurgeService()
    {
        var service = new RecordingPurgeService();
        using var worker = new AdminDeletionPurgeWorker(
            service,
            new StaticOptionsMonitor<AdminDeletionPurgeOptions>(new AdminDeletionPurgeOptions { Enabled = false }),
            NullLogger<AdminDeletionPurgeWorker>.Instance);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task DefaultEnabledWorker_PreservesImmediateFirstExecution()
    {
        var service = new RecordingPurgeService();
        using var worker = new AdminDeletionPurgeWorker(
            service,
            new StaticOptionsMonitor<AdminDeletionPurgeOptions>(new AdminDeletionPurgeOptions()),
            NullLogger<AdminDeletionPurgeWorker>.Instance);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await service.FirstCall.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task DevelopmentRuntimeProjection_ReportsPhaseAWorkersDisabled()
    {
        await using var factory = Factory(PhaseAConfiguration());
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/runtime-mode", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<ReviewSafeRuntimeStatus>(TestContext.Current.CancellationToken);
        Assert.NotNull(status);
        Assert.Equal("Development", status.Mode);
        Assert.False(status.NotificationDeliveryWorkerEnabled);
        Assert.False(status.NotificationEscalationWorkerEnabled);
        Assert.False(status.AdminDeletionPurgeWorkerEnabled);
        Assert.False(status.MutationWorkersEnabled);
        Assert.False(status.BackgroundWorkersEnabled);
    }

    private static QmsWebApplicationFactory Factory(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase)
        {
            ["DevAuthentication:Enabled"] = "true",
            ["DevelopmentData:SeedEnabled"] = "false",
            ["Database:ApplyMigrationsOnStartup"] = "false"
        };
        return QmsWebApplicationFactory.Create(
            Environments.Development,
            configuration,
            includeDefaultDevelopmentAuthentication: true);
    }

    private static Dictionary<string, string?> PhaseAConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["ReviewSafe:Enabled"] = "false",
            ["Notifications:Dispatch:Enabled"] = "false",
            ["Notifications:Escalation:Enabled"] = "false",
            ["AdminDeletionPurge:Enabled"] = "false"
        };
    }

    private static IConfiguration Configuration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class RecordingPurgeService : IAdminDeletionPurgeService
    {
        public int CallCount { get; private set; }
        public TaskCompletionSource FirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AdminScheduledDeletionPurgeResult> PurgeDueAsync(CancellationToken cancellationToken)
        {
            CallCount += 1;
            FirstCall.TrySetResult();
            return Task.FromResult(new AdminScheduledDeletionPurgeResult(0, 0, 0, 0));
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
