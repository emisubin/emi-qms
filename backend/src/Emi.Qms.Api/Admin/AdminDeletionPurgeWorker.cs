using Microsoft.Extensions.Options;
using Emi.Qms.Api.BusinessUnits;

namespace Emi.Qms.Api.Admin;

public sealed class AdminDeletionPurgeWorker(
    IAdminDeletionPurgeService? deletionService,
    IOptionsMonitor<AdminDeletionPurgeOptions> options,
    ILogger<AdminDeletionPurgeWorker> logger,
    BusinessUnitWorkerRunner? runner = null)
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
                if (runner is null)
                    await deletionService!.PurgeDueAsync(stoppingToken);
                else
                    await runner.RunAsync<IAdminDeletionPurgeService>(
                        target => target.Code == BusinessUnitCodes.Cheongju && target.AdminDeletionWorkerEnabled,
                        async (service, ct) => { await service.PurgeDueAsync(ct); }, stoppingToken);
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
