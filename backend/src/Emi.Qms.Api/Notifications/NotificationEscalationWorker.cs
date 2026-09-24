using Microsoft.Extensions.Options;
using Emi.Qms.Api.DeploymentMaintenance;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationEscalationWorker(
    NotificationEscalationService escalationService,
    IOptionsMonitor<NotificationOptions> options,
    ILogger<NotificationEscalationWorker> logger,
    DatabaseConnectionStringProvider? connections = null)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var currentOptions = options.CurrentValue.Escalation;
            if (currentOptions.Enabled)
            {
                try
                {
                    if (connections is null)
                        await escalationService.EvaluateAsync(stoppingToken);
                    else
                    {
                        await using var lease = await DeploymentMaintenanceLease.AcquireAsync(
                            connections, connections.BusinessUnits.Businesses, stoppingToken);
                        if (lease is not null) await escalationService.EvaluateAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Notification escalation worker failed without blocking application startup.");
                }
            }

            var interval = TimeSpan.FromSeconds(Math.Max(30, currentOptions.WorkerIntervalSeconds));
            await Task.Delay(interval, stoppingToken);
        }
    }
}
