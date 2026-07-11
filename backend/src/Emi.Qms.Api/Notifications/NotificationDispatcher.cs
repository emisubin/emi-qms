using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationDispatcher(
    NotificationDeliveryStore deliveryStore,
    IEnumerable<INotificationChannelHandler> channelHandlers,
    IOptionsMonitor<NotificationOptions> options,
    NotificationWorkerIdentity workerIdentity,
    ILogger<NotificationDispatcher> logger)
{
    private readonly IReadOnlyDictionary<string, INotificationChannelHandler> handlers =
        channelHandlers.ToDictionary(handler => handler.Channel, StringComparer.Ordinal);

    public async Task<NotificationDispatchSummary> DispatchAsync(CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        var created = await deliveryStore.CreateImmediateDeliveriesAsync(currentOptions, cancellationToken);
        var digests = await deliveryStore.CreateDailyDigestDeliveriesIfDueAsync(currentOptions, cancellationToken);
        var processed = await SendDueDeliveriesAsync(currentOptions, cancellationToken);
        return new NotificationDispatchSummary(created, digests, processed);
    }

    public async Task<int> SendDueDeliveriesAsync(NotificationOptions currentOptions, CancellationToken cancellationToken)
    {
        var leaseDuration = NotificationDeliveryLeasePolicy.GetValidatedLeaseDuration(currentOptions);
        var deliveries = await deliveryStore.ClaimDueDeliveriesAsync(
            Math.Max(1, currentOptions.Dispatch.MaxBatchSize),
            Math.Max(1, currentOptions.Dispatch.RetryCount),
            workerIdentity.InstanceId,
            leaseDuration,
            cancellationToken);

        var processed = 0;
        foreach (var claimed in deliveries)
        {
            try
            {
                await SendClaimedDeliveryAsync(claimed, currentOptions, null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            processed++;
        }

        return processed;
    }

    public async Task<NotificationChannelResult> DispatchDeliveryAsync(
        Guid deliveryId,
        NotificationDeliveryMessage? preparedMessage,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        var leaseDuration = NotificationDeliveryLeasePolicy.GetValidatedLeaseDuration(currentOptions);
        var claimed = await deliveryStore.ClaimDeliveryAsync(
            deliveryId,
            Math.Max(1, retryCount),
            workerIdentity.InstanceId,
            leaseDuration,
            cancellationToken);
        if (claimed is null)
        {
            return NotificationChannelResult.Failed(
                "NotificationDeliveryClaimUnavailable",
                "알림 발송 요청을 claim할 수 없습니다.");
        }

        return await SendClaimedDeliveryAsync(claimed, currentOptions, preparedMessage, cancellationToken, retryCount);
    }

    public async Task<NotificationChannelResult> SendClaimedDeliveryAsync(
        ClaimedNotificationDelivery claimed,
        NotificationOptions currentOptions,
        NotificationDeliveryMessage? preparedMessage,
        CancellationToken cancellationToken,
        int? retryCountOverride = null)
    {
        var retryCount = Math.Max(1, retryCountOverride ?? currentOptions.Dispatch.RetryCount);
        var delivery = claimed.Delivery;
        if (!handlers.TryGetValue(delivery.Channel, out var handler))
        {
            var disabled = NotificationChannelResult.Disabled(
                "ChannelHandlerMissing",
                "알림 채널 핸들러가 등록되어 있지 않습니다.");
            await CompleteAsync(claimed, disabled, retryCount, cancellationToken);
            return disabled;
        }

        try
        {
            var message = preparedMessage ?? await deliveryStore.RenderMessageAsync(delivery, cancellationToken);
            if (handler.WillCallExternalProvider(message))
            {
                var auditRecorded = await deliveryStore.MarkProviderCallStartedAsync(
                    delivery.DeliveryId,
                    claimed.ClaimToken,
                    cancellationToken);
                if (!auditRecorded)
                {
                    var claimLost = NotificationChannelResult.Failed(
                        "NotificationDeliveryClaimLost",
                        "Provider 호출 전에 claim 소유권을 확인할 수 없습니다.");
                    await CompleteAsync(claimed, claimLost, retryCount, cancellationToken);
                    return claimLost;
                }
            }

            var result = await handler.SendAsync(message, cancellationToken);
            await CompleteAsync(claimed, result, retryCount, cancellationToken);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Notification delivery attempt failed with stable code NotificationDeliveryFailed.");
            var failure = NotificationChannelResult.Failed(
                "NotificationDeliveryFailed",
                "알림 외부 채널 발송 처리 중 오류가 발생했습니다.");
            await CompleteAsync(claimed, failure, retryCount, cancellationToken);
            return failure;
        }
    }

    private async Task CompleteAsync(
        ClaimedNotificationDelivery claimed,
        NotificationChannelResult result,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var completed = await deliveryStore.CompleteDeliveryAttemptAsync(
            claimed.Delivery.DeliveryId,
            claimed.ClaimToken,
            result,
            retryCount,
            cancellationToken);
        if (!completed)
        {
            logger.LogWarning("Notification delivery completion was fenced with stable code NotificationDeliveryClaimLost.");
        }
    }
}
