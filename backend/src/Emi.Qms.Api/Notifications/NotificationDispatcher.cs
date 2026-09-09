using Emi.Qms.Api.BusinessUnits;
using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationDispatcher(
    NotificationDeliveryStore deliveryStore,
    IEnumerable<INotificationChannelHandler> channelHandlers,
    IOptionsMonitor<NotificationOptions> options,
    NotificationWorkerIdentity workerIdentity,
    DatabaseConnectionStringProvider connectionStringProvider,
    BusinessUnitDatabaseBoundaryValidator boundaryValidator,
    ILogger<NotificationDispatcher> logger)
{
    private readonly IReadOnlyDictionary<string, INotificationChannelHandler> handlers =
        channelHandlers.ToDictionary(handler => handler.Channel, StringComparer.Ordinal);

    public async Task<NotificationDispatchSummary> DispatchAsync(CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        if (!connectionStringProvider.BusinessUnits.Enabled)
        {
            return await DispatchTargetAsync(currentOptions, target: null, cancellationToken);
        }

        var created = 0;
        var digests = 0;
        var processed = 0;
        var failures = 0;
        foreach (var target in connectionStringProvider.BusinessUnits.Businesses
                     .Where(candidate => candidate.ExternalNotificationsEnabled))
        {
            try
            {
                await boundaryValidator.ValidateAsync(target, cancellationToken);
                var summary = await DispatchTargetAsync(currentOptions, target, cancellationToken);
                created += summary.CreatedDeliveryCount;
                digests += summary.CreatedDigestDeliveryCount;
                processed += summary.ProcessedDeliveryCount;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures++;
                logger.LogError(
                    "Notification dispatch target failed. Target={Target} ExceptionType={ExceptionType}.",
                    target.Code,
                    exception.GetType().Name);
            }
        }

        if (failures > 0)
        {
            throw new InvalidOperationException(
                $"Notification dispatch failed for {failures} target(s); no target fallback was used.");
        }
        return new NotificationDispatchSummary(created, digests, processed);
    }

    private async Task<NotificationDispatchSummary> DispatchTargetAsync(
        NotificationOptions currentOptions,
        BusinessUnitDatabaseTarget? target,
        CancellationToken cancellationToken)
    {
        var created = await deliveryStore.CreateImmediateDeliveriesAsync(currentOptions, cancellationToken, target);
        var digests = await deliveryStore.CreateDailyDigestDeliveriesIfDueAsync(currentOptions, cancellationToken, target);
        var processed = await SendDueDeliveriesAsync(currentOptions, cancellationToken, target);
        return new NotificationDispatchSummary(created, digests, processed);
    }

    public async Task<int> SendDueDeliveriesAsync(
        NotificationOptions currentOptions,
        CancellationToken cancellationToken,
        BusinessUnitDatabaseTarget? target = null)
    {
        if (connectionStringProvider.BusinessUnits.Enabled
            && !connectionStringProvider.ExternalNotificationsEnabled(target))
        {
            return 0;
        }
        var leaseDuration = NotificationDeliveryLeasePolicy.GetValidatedLeaseDuration(currentOptions);
        var deliveries = await deliveryStore.ClaimDueDeliveriesAsync(
            Math.Max(1, currentOptions.Dispatch.MaxBatchSize),
            Math.Max(1, currentOptions.Dispatch.RetryCount),
            workerIdentity.InstanceId,
            leaseDuration,
            cancellationToken,
            target);

        var processed = 0;
        foreach (var claimed in deliveries)
        {
            try
            {
                await SendClaimedDeliveryAsync(claimed, currentOptions, null, cancellationToken, target: target);
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
        var target = connectionStringProvider.GetCurrentBusinessUnit();
        if (connectionStringProvider.BusinessUnits.Enabled
            && !connectionStringProvider.ExternalNotificationsEnabled(target))
        {
            return NotificationChannelResult.Disabled(
                "BusinessUnitExternalNotificationsDisabled",
                "이 사업장의 외부 알림 발송은 비활성화되어 있습니다.");
        }
        var leaseDuration = NotificationDeliveryLeasePolicy.GetValidatedLeaseDuration(currentOptions);
        var claimed = await deliveryStore.ClaimDeliveryAsync(
            deliveryId,
            Math.Max(1, retryCount),
            workerIdentity.InstanceId,
            leaseDuration,
            cancellationToken,
            target);
        if (claimed is null)
        {
            return NotificationChannelResult.Failed(
                "NotificationDeliveryClaimUnavailable",
                "알림 발송 요청을 claim할 수 없습니다.");
        }

        return await SendClaimedDeliveryAsync(
            claimed,
            currentOptions,
            preparedMessage,
            cancellationToken,
            retryCount,
            target);
    }

    public async Task<NotificationChannelResult> SendClaimedDeliveryAsync(
        ClaimedNotificationDelivery claimed,
        NotificationOptions currentOptions,
        NotificationDeliveryMessage? preparedMessage,
        CancellationToken cancellationToken,
        int? retryCountOverride = null,
        BusinessUnitDatabaseTarget? target = null)
    {
        if (connectionStringProvider.BusinessUnits.Enabled
            && !connectionStringProvider.ExternalNotificationsEnabled(target))
        {
            return NotificationChannelResult.Disabled(
                "BusinessUnitExternalNotificationsDisabled",
                "이 사업장의 외부 알림 발송은 비활성화되어 있습니다.");
        }
        var retryCount = Math.Max(1, retryCountOverride ?? currentOptions.Dispatch.RetryCount);
        var delivery = claimed.Delivery;
        if (!handlers.TryGetValue(delivery.Channel, out var handler))
        {
            var disabled = NotificationChannelResult.Disabled(
                "ChannelHandlerMissing",
                "알림 채널 핸들러가 등록되어 있지 않습니다.");
            await CompleteAsync(claimed, disabled, retryCount, cancellationToken, target);
            return disabled;
        }

        try
        {
            var message = preparedMessage ?? await deliveryStore.RenderMessageAsync(delivery, cancellationToken, target);
            NotificationChannelResult result;
            if (handler is IProviderCallAwareNotificationChannelHandler providerCallAwareHandler)
            {
                result = await providerCallAwareHandler.SendAsync(
                    message,
                    ct => deliveryStore.MarkProviderCallStartedAsync(
                        delivery.DeliveryId,
                        claimed.ClaimToken,
                        ct,
                        target),
                    cancellationToken);
            }
            else
            {
                if (handler.WillCallExternalProvider(message))
                {
                    var auditRecorded = await deliveryStore.MarkProviderCallStartedAsync(
                        delivery.DeliveryId,
                        claimed.ClaimToken,
                        cancellationToken,
                        target);
                    if (!auditRecorded)
                    {
                        var claimLost = NotificationChannelResult.Failed(
                            "NotificationDeliveryClaimLost",
                            "Provider 호출 전에 claim 소유권을 확인할 수 없습니다.");
                        await CompleteAsync(claimed, claimLost, retryCount, cancellationToken, target);
                        return claimLost;
                    }
                }

                result = await handler.SendAsync(message, cancellationToken);
            }
            await CompleteAsync(claimed, result, retryCount, cancellationToken, target);
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
            await CompleteAsync(claimed, failure, retryCount, cancellationToken, target);
            return failure;
        }
    }

    private async Task CompleteAsync(
        ClaimedNotificationDelivery claimed,
        NotificationChannelResult result,
        int retryCount,
        CancellationToken cancellationToken,
        BusinessUnitDatabaseTarget? target)
    {
        var completed = await deliveryStore.CompleteDeliveryAttemptAsync(
            claimed.Delivery.DeliveryId,
            claimed.ClaimToken,
            result,
            retryCount,
            cancellationToken,
            target);
        if (!completed)
        {
            logger.LogWarning("Notification delivery completion was fenced with stable code NotificationDeliveryClaimLost.");
        }
    }
}
