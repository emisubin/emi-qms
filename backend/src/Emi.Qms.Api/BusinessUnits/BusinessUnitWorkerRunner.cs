using Emi.Qms.Api.DeploymentMaintenance;

namespace Emi.Qms.Api.BusinessUnits;

/// <summary>Host orchestration owns target selection; each worker gets its own fixed scope and lease.</summary>
public sealed class BusinessUnitWorkerRunner(
    DatabaseConnectionStringProvider connections,
    IServiceScopeFactory scopes,
    ILogger<BusinessUnitWorkerRunner> logger)
{
    public Task RunAsync<T>(Func<BusinessUnitDatabaseTarget, bool> enabled,
        Func<T, CancellationToken, Task> run, CancellationToken ct) where T : notnull =>
        Task.WhenAll(connections.BusinessUnits.Businesses.Where(enabled).Select(async target =>
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<BusinessDatabaseScope>().Bind(target);
                await using var lease = await DeploymentMaintenanceLease.AcquireAsync(connections, [target], ct);
                if (lease is not null)
                    await run(scope.ServiceProvider.GetRequiredService<T>(), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning("Business worker failed. Target={Target} Worker={Worker} ErrorType={ErrorType}",
                    target.Code, typeof(T).Name, exception.GetType().Name);
            }
        }));
}
