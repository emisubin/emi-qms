using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebPush;

namespace Emi.Qms.Api.Notifications;

public sealed record WebPushProtocolRequest(
    string Endpoint,
    string P256dh,
    string Auth,
    string Payload,
    string Subject,
    string PublicKey,
    string PrivateKey);

public interface IWebPushProtocolClient
{
    Task SendAsync(WebPushProtocolRequest request, CancellationToken cancellationToken);
}

public sealed class WebPushProtocolException(int? statusCode, string errorCode, Exception? innerException = null)
    : Exception(errorCode, innerException)
{
    public int? StatusCode { get; } = statusCode;

    public string ErrorCode { get; } = errorCode;
}

public sealed class WebPushProtocolClient : IWebPushProtocolClient
{
    public async Task SendAsync(WebPushProtocolRequest request, CancellationToken cancellationToken)
    {
        using var client = new WebPushClient();
        try
        {
            await client.SendNotificationAsync(
                new PushSubscription(request.Endpoint, request.P256dh, request.Auth),
                request.Payload,
                new VapidDetails(request.Subject, request.PublicKey, request.PrivateKey),
                cancellationToken);
        }
        catch (WebPushException exception)
        {
            throw new WebPushProtocolException((int?)exception.StatusCode, "WebPushProviderRejected", exception);
        }
    }
}

public sealed class WebPushChannelHandler(
    IOptionsMonitor<NotificationOptions> options,
    IWebPushSubscriptionDeliveryStore subscriptionStore,
    IWebPushProtocolClient protocolClient,
    ILogger<WebPushChannelHandler> logger) : INotificationChannelHandler, IProviderCallAwareNotificationChannelHandler
{
    public string Channel => NotificationDeliveryChannels.WebPush;

    public bool WillCallExternalProvider(NotificationDeliveryMessage message)
    {
        var webPush = options.CurrentValue.WebPush;
        return webPush.Enabled
            && !webPush.DryRun
            && HasKeys(webPush);
    }

    public async Task<NotificationChannelResult> SendAsync(
        NotificationDeliveryMessage message,
        CancellationToken cancellationToken)
    {
        return await SendCoreAsync(message, null, cancellationToken);
    }

    public async Task<NotificationChannelResult> SendAsync(
        NotificationDeliveryMessage message,
        Func<CancellationToken, Task<bool>> markProviderCallStarted,
        CancellationToken cancellationToken)
    {
        return await SendCoreAsync(message, markProviderCallStarted, cancellationToken);
    }

    private async Task<NotificationChannelResult> SendCoreAsync(
        NotificationDeliveryMessage message,
        Func<CancellationToken, Task<bool>>? markProviderCallStarted,
        CancellationToken cancellationToken)
    {
        var webPush = options.CurrentValue.WebPush;
        if (!webPush.Enabled)
        {
            return NotificationChannelResult.Disabled("WebPushDisabled", "PWA 푸시 발송이 비활성화되어 있습니다.");
        }

        var target = await subscriptionStore.GetDeliveryTargetAsync(message.DeliveryId, cancellationToken);
        if (target is null)
        {
            return NotificationChannelResult.Suppressed("WebPushSubscriptionMissing", "푸시 대상 기기를 찾을 수 없습니다.");
        }

        if (!target.SubscriptionIsActive || !target.UserIsActive)
        {
            return NotificationChannelResult.Suppressed("WebPushSubscriptionInactive", "비활성 사용자 또는 기기는 푸시 대상에서 제외됩니다.");
        }

        if (!WebPushEndpointPolicy.IsAllowed(target.Endpoint, webPush))
        {
            await subscriptionStore.DeactivateForProviderAsync(
                target.SubscriptionId,
                target.Generation,
                "WebPushEndpointDisallowed",
                cancellationToken);
            return NotificationChannelResult.Suppressed(
                "WebPushEndpointDisallowed",
                "승인된 Web Push 서비스 주소가 아니어서 해당 기기 연결을 해제했습니다.");
        }

        if (!WebPushKeyValidation.IsValidSubscriptionKeys(target.P256dh, target.Auth))
        {
            await subscriptionStore.DeactivateForProviderAsync(
                target.SubscriptionId,
                target.Generation,
                "WebPushSubscriptionKeysInvalid",
                cancellationToken);
            return NotificationChannelResult.Suppressed(
                "WebPushSubscriptionKeysInvalid",
                "푸시 암호화 키가 올바르지 않아 해당 기기 연결을 해제했습니다.");
        }

        if (webPush.DryRun)
        {
            return NotificationChannelResult.DryRunSent();
        }

        if (!HasKeys(webPush))
        {
            return NotificationChannelResult.Disabled("WebPushVapidMissing", "PWA 푸시 발송 키가 설정되지 않았습니다.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            title = message.Subject,
            body = message.Body,
            url = message.LinkUrl ?? "/notifications",
            tag = $"emi-pms-notification-{message.DeliveryId:N}",
            icon = "/icons/emi-qms-192.png",
            badge = "/icons/favicon-32.png"
        });

        try
        {
            if (markProviderCallStarted is not null
                && !await markProviderCallStarted(cancellationToken))
            {
                return NotificationChannelResult.Failed(
                    "NotificationDeliveryClaimLost",
                    "Provider 호출 전에 claim 소유권을 확인할 수 없습니다.");
            }

            await protocolClient.SendAsync(
                new WebPushProtocolRequest(
                    target.Endpoint,
                    target.P256dh,
                    target.Auth,
                    payload,
                    webPush.Subject.Trim(),
                    webPush.PublicKey!.Trim(),
                    webPush.PrivateKey!.Trim()),
                cancellationToken);
            await subscriptionStore.RecordProviderAcceptedAsync(target.SubscriptionId, target.Generation, cancellationToken);
            return NotificationChannelResult.Sent("web-push-accepted");
        }
        catch (WebPushProtocolException exception) when (IsPermanent(exception.StatusCode))
        {
            var code = exception.StatusCode is null ? "WebPushPermanentFailure" : $"WebPushHttp{exception.StatusCode}";
            await subscriptionStore.DeactivateForProviderAsync(target.SubscriptionId, target.Generation, code, cancellationToken);
            logger.LogInformation("Web Push subscription {SubscriptionId} was deactivated after a permanent provider response.", target.SubscriptionId);
            return NotificationChannelResult.Suppressed(code, "푸시 서비스에서 만료되거나 유효하지 않은 기기 구독으로 응답했습니다.");
        }
        catch (WebPushProtocolException exception)
        {
            var code = exception.StatusCode is null ? exception.ErrorCode : $"WebPushHttp{exception.StatusCode}";
            await subscriptionStore.RecordProviderFailureAsync(target.SubscriptionId, target.Generation, code, cancellationToken);
            logger.LogWarning("Web Push provider call failed with {ErrorCode}.", code);
            return NotificationChannelResult.Failed(code, "PWA 푸시 서비스 요청이 실패했습니다.");
        }
        catch (HttpRequestException exception)
        {
            await subscriptionStore.RecordProviderFailureAsync(target.SubscriptionId, target.Generation, "WebPushNetworkFailure", cancellationToken);
            logger.LogWarning("Web Push provider call failed with exception type {ExceptionType}.", exception.GetType().Name);
            return NotificationChannelResult.Failed("WebPushNetworkFailure", "PWA 푸시 서비스 연결에 실패했습니다.");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await subscriptionStore.RecordProviderFailureAsync(target.SubscriptionId, target.Generation, "WebPushTimeout", cancellationToken);
            logger.LogWarning("Web Push provider call timed out with exception type {ExceptionType}.", exception.GetType().Name);
            return NotificationChannelResult.Failed("WebPushTimeout", "PWA 푸시 서비스 요청 시간이 초과되었습니다.");
        }
    }

    private static bool HasKeys(NotificationWebPushOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.Subject)
            && !string.IsNullOrWhiteSpace(options.PublicKey)
            && !string.IsNullOrWhiteSpace(options.PrivateKey);
    }

    private static bool IsPermanent(int? statusCode)
    {
        return statusCode is 404 or 410
            || statusCode is >= 400 and < 500 and not 408 and not 425 and not 429;
    }
}
