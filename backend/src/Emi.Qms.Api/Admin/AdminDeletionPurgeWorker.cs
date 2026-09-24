using Microsoft.Extensions.Options;
using Emi.Qms.Api.DeploymentMaintenance;

namespace Emi.Qms.Api.Admin;

public sealed class AdminDeletionPurgeWorker(
    IAdminDeletionPurgeService deletionService,
    IOptionsMonitor<AdminDeletionPurgeOptions> options,
    ILogger<AdminDeletionPurgeWorker> logger,
    DatabaseConnectionStringProvider? connections = null)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.Enabled)
        {
            logger.LogInformation("Administrator deletion purge worker is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!options.CurrentValue.Enabled)
            {
                logger.LogInformation("Administrator deletion purge worker stopped because it was disabled by configuration.");
                return;
            }

            try
            {
                if (connections is null)
                    await deletionService.PurgeDueAsync(stoppingToken);
                else
                {
                    await using var lease = await DeploymentMaintenanceLease.AcquireAsync(
                        connections, connections.BusinessUnits.Businesses, stoppingToken);
                    if (lease is not null) await deletionService.PurgeDueAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled administrator deletion purge failed.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
