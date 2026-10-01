using Microsoft.Extensions.Options;
using Emi.Qms.Api.BusinessUnits;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationEscalationWorker(BusinessUnitWorkerRunner runner, IOptionsMonitor<NotificationOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.CurrentValue.Escalation.Enabled)
                await runner.RunAsync<NotificationEscalationService>(target => target.Code == BusinessUnitCodes.Cheongju && target.EscalationWorkerEnabled && target.ExternalNotificationsEnabled,
                    async (service, ct) => { await service.EvaluateAsync(ct); }, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(30, options.CurrentValue.Escalation.WorkerIntervalSeconds)), stoppingToken);
        }
    }
}
