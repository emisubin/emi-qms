using Emi.Qms.Api.ReviewSafe;
namespace Emi.Qms.Api.G2;
public sealed class G2MaterialRetentionWorker(IServiceScopeFactory scopes,IConfiguration config,TimeProvider time,ILogger<G2MaterialRetentionWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateOnly? completed=null;
        while(!stoppingToken.IsCancellationRequested)
        {
            var today=G2MaterialRules.Today(time);
            if(completed!=today && MutationWorkerActivationPolicy.Evaluate(config, ReviewSafeMode.IsEnabled(config)).G2MaterialRetentionWorkerEnabled)
            {
                try {using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<G2MaterialStore>().PruneAsync(stoppingToken);completed=today;}
                catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
                catch(Exception e){logger.LogError(e,"G2 material retention did not complete; retrying later.");}
            }
            await Task.Delay(TimeSpan.FromHours(1),time,stoppingToken);
        }
    }
}
