using Emi.Qms.Api.Notifications;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class WebPushChannelHandlerTests
{
    private const string ValidPublicKey = "BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string ValidPrivateKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAE";
    private const string ValidAuthSecret = "AAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public async Task DryRun_UsesActiveDeviceWithoutCallingProvider()
    {
        var store = new FakeStore { Target = ActiveTarget() };
        var client = new FakeClient();
        var handler = CreateHandler(new NotificationWebPushOptions
        {
            Enabled = true,
            DryRun = true,
            PublicKey = ValidPublicKey,
            AllowedEndpointHostSuffixes = ["example.test"]
        }, store, client);

        var result = await handler.SendAsync(Message(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.DryRunSent, result.Status);
        Assert.Equal(0, client.CallCount);
        Assert.Equal(0, store.AcceptedCount);
    }

    [Fact]
    public async Task AcceptedProviderResponse_RecordsDeviceSuccess()
    {
        var store = new FakeStore { Target = ActiveTarget() };
        var client = new FakeClient();
        var handler = CreateHandler(ActualOptions(), store, client);

        var result = await handler.SendAsync(Message(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Sent, result.Status);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(1, store.AcceptedCount);
        using var payload = JsonDocument.Parse(client.LastRequest!.Payload);
        Assert.Equal("업무 알림", payload.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("secret-auth", client.LastRequest!.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoneProviderResponse_DeactivatesOnlyTargetDevice()
    {
        var store = new FakeStore { Target = ActiveTarget() };
        var client = new FakeClient { Exception = new WebPushProtocolException(410, "gone") };
        var handler = CreateHandler(ActualOptions(), store, client);

        var result = await handler.SendAsync(Message(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Suppressed, result.Status);
        Assert.Equal("WebPushHttp410", store.DeactivatedReason);
        Assert.Equal(0, store.FailureCount);
    }

    [Fact]
    public async Task ServiceUnavailable_RemainsRetryableAndRecordsFailure()
    {
        var store = new FakeStore { Target = ActiveTarget() };
        var client = new FakeClient { Exception = new WebPushProtocolException(503, "unavailable") };
        var handler = CreateHandler(ActualOptions(), store, client);

        var result = await handler.SendAsync(Message(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.Equal("WebPushHttp503", result.ErrorCode);
        Assert.Equal(1, store.FailureCount);
        Assert.Null(store.DeactivatedReason);
    }

    [Fact]
    public async Task TooManyRequests_RemainsRetryableAndKeepsSubscriptionActive()
    {
        var store = new FakeStore { Target = ActiveTarget() };
        var client = new FakeClient { Exception = new WebPushProtocolException(429, "rate limited") };
        var handler = CreateHandler(ActualOptions(), store, client);

        var result = await handler.SendAsync(Message(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.Equal("WebPushHttp429", result.ErrorCode);
        Assert.Equal(1, store.FailureCount);
        Assert.Null(store.DeactivatedReason);
    }

    [Fact]
    public async Task ProviderTimeout_RemainsRetryableAndKeepsSubscriptionActive()
    {
        var store = new FakeStore { Target = ActiveTarget() };
        var client = new FakeClient { Exception = new TaskCanceledException("synthetic timeout") };
        var handler = CreateHandler(ActualOptions(), store, client);

        var result = await handler.SendAsync(Message(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.Equal("WebPushTimeout", result.ErrorCode);
        Assert.Equal(1, store.FailureCount);
        Assert.Null(store.DeactivatedReason);
    }

    [Fact]
    public async Task DisallowedEndpoint_IsDeactivatedWithoutCallingProvider()
    {
        var store = new FakeStore
        {
            Target = ActiveTarget() with { Endpoint = "https://127.0.0.1/internal" }
        };
        var client = new FakeClient();
        var handler = CreateHandler(ActualOptions(), store, client);

        var result = await handler.SendAsync(Message(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Suppressed, result.Status);
        Assert.Equal("WebPushEndpointDisallowed", result.ErrorCode);
        Assert.Equal("WebPushEndpointDisallowed", store.DeactivatedReason);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task SuppressedTarget_DoesNotRecordProviderCallStart()
    {
        var store = new FakeStore
        {
            Target = ActiveTarget() with { Endpoint = "https://127.0.0.1/internal" }
        };
        var client = new FakeClient();
        var handler = CreateHandler(ActualOptions(), store, client);
        var markerCount = 0;

        var result = await handler.SendAsync(
            Message(),
            _ =>
            {
                markerCount++;
                return Task.FromResult(true);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Suppressed, result.Status);
        Assert.Equal(0, markerCount);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task NetworkExceptionLogDoesNotContainSubscriptionEndpoint()
    {
        const string endpointToken = "subscription-secret-token";
        var store = new FakeStore
        {
            Target = ActiveTarget() with { Endpoint = $"https://push.example.test/{endpointToken}" }
        };
        var client = new FakeClient
        {
            Exception = new HttpRequestException($"Request to https://push.example.test/{endpointToken} failed")
        };
        var logger = new CaptureLogger();
        var handler = CreateHandler(ActualOptions(), store, client, logger);

        var result = await handler.SendAsync(Message(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.DoesNotContain(endpointToken, string.Join('\n', logger.Entries), StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedVapidPublicKey_FailsStartupValidation()
    {
        var result = new NotificationOptionsValidator().Validate(null, new NotificationOptions
        {
            WebPush = new NotificationWebPushOptions
            {
                Enabled = true,
                DryRun = true,
                PublicKey = "not-base64url"
            }
        });

        Assert.True(result.Failed);
    }

    private static WebPushChannelHandler CreateHandler(
        NotificationWebPushOptions webPush,
        FakeStore store,
        FakeClient client,
        ILogger<WebPushChannelHandler>? logger = null)
    {
        return new WebPushChannelHandler(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions { WebPush = webPush }),
            store,
            client,
            logger ?? NullLogger<WebPushChannelHandler>.Instance);
    }

    private static NotificationWebPushOptions ActualOptions() => new()
    {
        Enabled = true,
        DryRun = false,
        Subject = "mailto:pms@emiinc.co.kr",
        PublicKey = ValidPublicKey,
        PrivateKey = ValidPrivateKey,
        AllowedEndpointHostSuffixes = ["example.test"]
    };

    private static NotificationDeliveryMessage Message() => new(
        Guid.NewGuid(),
        NotificationDeliveryChannels.WebPush,
        NotificationDeliveryTypes.WebPushNotification,
        "업무 알림",
        "EMI PMS에서 알림 내용을 확인해 주세요.",
        "/teams/activity/notifications/10000000-0000-0000-0000-000000000001",
        "검수 사용자",
        "user@example.com");

    private static WebPushDeliveryTarget ActiveTarget() => new(
        Guid.NewGuid(),
        1,
        "https://push.example.test/subscription-secret",
        ValidPublicKey,
        ValidAuthSecret,
        true,
        true);

    private sealed class FakeClient : IWebPushProtocolClient
    {
        public int CallCount { get; private set; }
        public WebPushProtocolRequest? LastRequest { get; private set; }
        public Exception? Exception { get; init; }

        public Task SendAsync(WebPushProtocolRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }

    private sealed class FakeStore : IWebPushSubscriptionDeliveryStore
    {
        public WebPushDeliveryTarget? Target { get; init; }
        public int AcceptedCount { get; private set; }
        public int FailureCount { get; private set; }
        public string? DeactivatedReason { get; private set; }

        public Task<WebPushDeliveryTarget?> GetDeliveryTargetAsync(Guid deliveryId, CancellationToken cancellationToken) => Task.FromResult(Target);

        public Task RecordProviderAcceptedAsync(Guid subscriptionId, long expectedGeneration, CancellationToken cancellationToken)
        {
            AcceptedCount++;
            return Task.CompletedTask;
        }

        public Task RecordProviderFailureAsync(Guid subscriptionId, long expectedGeneration, string failureCode, CancellationToken cancellationToken)
        {
            FailureCount++;
            return Task.CompletedTask;
        }

        public Task DeactivateForProviderAsync(Guid subscriptionId, long expectedGeneration, string reason, CancellationToken cancellationToken)
        {
            DeactivatedReason = reason;
            return Task.CompletedTask;
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class CaptureLogger : ILogger<WebPushChannelHandler>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(formatter(state, exception));
            if (exception is not null)
            {
                Entries.Add(exception.ToString());
            }
        }
    }
}
