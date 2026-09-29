using Microsoft.Extensions.Options;
using Emi.Qms.Api.BusinessUnits;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationDeliveryWorker(BusinessUnitWorkerRunner runner, IOptionsMonitor<NotificationOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.CurrentValue.Dispatch.Enabled)
                await runner.RunAsync<NotificationDispatcher>(target => target.ExternalNotificationsEnabled,
                    async (service, ct) => { await service.DispatchAsync(ct); }, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, options.CurrentValue.Dispatch.WorkerIntervalSeconds)), stoppingToken);
        }
    }
}
