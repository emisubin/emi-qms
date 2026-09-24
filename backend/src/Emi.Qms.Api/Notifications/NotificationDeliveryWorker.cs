using Microsoft.Extensions.Options;
using Emi.Qms.Api.DeploymentMaintenance;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationDeliveryWorker(
    NotificationDispatcher dispatcher,
    IOptionsMonitor<NotificationOptions> options,
    ILogger<NotificationDeliveryWorker> logger,
    DatabaseConnectionStringProvider? connections = null)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var currentOptions = options.CurrentValue;
            if (currentOptions.Dispatch.Enabled)
            {
                try
                {
                    if (connections is null)
                        await dispatcher.DispatchAsync(stoppingToken);
                    else
                    {
                        await using var lease = await DeploymentMaintenanceLease.AcquireAsync(
                            connections, connections.BusinessUnits.Businesses, stoppingToken);
                        if (lease is not null) await dispatcher.DispatchAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Notification delivery worker failed without blocking application startup.");
                }
            }

            var interval = TimeSpan.FromSeconds(Math.Max(5, currentOptions.Dispatch.WorkerIntervalSeconds));
            await Task.Delay(interval, stoppingToken);
        }
    }
}
