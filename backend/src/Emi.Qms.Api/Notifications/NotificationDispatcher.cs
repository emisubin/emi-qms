using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationDispatcher(
    NotificationDeliveryStore deliveryStore,
    IEnumerable<INotificationChannelHandler> channelHandlers,
    IOptionsMonitor<NotificationOptions> options,
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
        var deliveries = await deliveryStore.GetDueDeliveriesAsync(
            Math.Max(1, currentOptions.Dispatch.MaxBatchSize),
            Math.Max(1, currentOptions.Dispatch.RetryCount),
            cancellationToken);

        var processed = 0;
        foreach (var delivery in deliveries)
        {
            if (!handlers.TryGetValue(delivery.Channel, out var handler))
            {
                await deliveryStore.MarkDeliveryResultAsync(
                    delivery.DeliveryId,
                    NotificationChannelResult.Disabled("ChannelHandlerMissing", "알림 채널 핸들러가 등록되어 있지 않습니다."),
                    currentOptions.Dispatch.RetryCount,
                    cancellationToken);
                processed++;
                continue;
            }

            try
            {
                var message = await deliveryStore.RenderMessageAsync(delivery, cancellationToken);
                var result = await handler.SendAsync(message, cancellationToken);
                await deliveryStore.MarkDeliveryResultAsync(delivery.DeliveryId, result, currentOptions.Dispatch.RetryCount, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Notification delivery {DeliveryId} failed without blocking workflow.", delivery.DeliveryId);
                await deliveryStore.MarkDeliveryResultAsync(
                    delivery.DeliveryId,
                    NotificationChannelResult.Failed("NotificationDeliveryFailed", "알림 외부 채널 발송 처리 중 오류가 발생했습니다."),
                    currentOptions.Dispatch.RetryCount,
                    cancellationToken);
            }

            processed++;
        }

        return processed;
    }
}
