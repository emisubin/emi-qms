using Emi.Qms.Api.BusinessUnits;
using Npgsql;

namespace Emi.Qms.Api.InteriorBusbar;

internal sealed class InteriorBusbarEcountWorker(
    DatabaseConnectionStringProvider connections, InteriorBusbarEcountOptions options,
    IInteriorBusbarEcountClient client, TimeProvider clock, ILogger<InteriorBusbarEcountWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("이카운트 전송 처리 보류. 연결 및 대기 상태를 확인하세요."); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    internal async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled) return false;
        var target = connections.BusinessUnits.Businesses.Single(t => t.Code == BusinessUnitCodes.Cheongju);
        var connectionString = connections.GetConnectionString(target);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        if (!await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, target, cancellationToken))
            throw new BusinessUnitContextUnavailableException("busbar_ecount_database_identity_mismatch");
        await using var mutex = new NpgsqlCommand("select pg_try_advisory_lock(9070095)", connection);
        if (!(bool)(await mutex.ExecuteScalarAsync(cancellationToken))!) return false;
        try
        {
            // A worker has no request business-unit context. Resolve and verify Cheongju above,
            // then bind the existing transaction store to precisely that connection.
            var fixedConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["ConnectionStrings:QmsDatabase"] = connectionString
            }).Build();
            var store = new InteriorBusbarStore(new(fixedConfiguration), clock, ecountOptions: options);
            await store.RecoverEcountAttempts();
            var runtime = (await InteriorBusbarStore.Rows(connection, "select * from busbar_ecount_runtime"))[0];
            if (runtime["environment"] is string bound && bound != options.Environment || runtime["companyCode"] is string company && company != options.CompanyCode)
            {
                await InteriorBusbarStore.Exec(connection, "update busbar_ecount_runtime set paused=true,message='전송 환경 또는 회사가 다름 · 전용 DB 확인 필요'");
                return false;
            }
            if (runtime["environment"] is null)
                await InteriorBusbarStore.Exec(connection, "update busbar_ecount_runtime set environment=@environment,company_code=@company", ("environment", options.Environment), ("company", options.CompanyCode));
            if ((bool)runtime["paused"]! || runtime["nextSendAtUtc"] is DateTime next && next > clock.GetUtcNow().UtcDateTime) return false;
            var jobs = await InteriorBusbarStore.Rows(connection, "select id from busbar_ecount_jobs where state='Pending' and not needs_review order by created_at_utc,id limit 1");
            if (jobs.Count == 0) return false;
            if (!client.HasSession)
            {
                var interval = options.Environment == "Test" ? TimeSpan.FromSeconds(10) : TimeSpan.FromMinutes(10);
                if (runtime["lastLoginAtUtc"] is DateTime last && clock.GetUtcNow().UtcDateTime < last + interval) return false;
                // Persist the circuit before authentication. A process crash or rejected credentials
                // cannot trigger repeated login attempts after a restart.
                await InteriorBusbarStore.Exec(connection, "update busbar_ecount_runtime set paused=true,message='인증 확인 중단 또는 실패 · 설정 확인 후 재개 필요',last_login_at_utc=@now", ("now", clock.GetUtcNow()));
                using var authTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                authTimeout.CancelAfter(TimeSpan.FromSeconds(45));
                if (!await client.AuthenticateAsync(authTimeout.Token)) return false;
                await InteriorBusbarStore.Exec(connection, "update busbar_ecount_runtime set paused=false,message=null");
            }
            var attempt = await store.ClaimEcountJob((Guid)jobs[0]["id"]!);
            if (attempt is null) return true;
            await InteriorBusbarStore.Exec(connection, "update busbar_ecount_runtime set next_send_at_utc=@next", ("next", clock.GetUtcNow().AddSeconds(11)));
            BusbarEcountResult result;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(25));
                result = await client.SendAsync(attempt, timeout.Token);
            }
            catch (Exception) { result = new("Unknown"); }
            await store.FinishEcountAttempt(attempt.Id, result);
            return true;
        }
        finally
        {
            await using var release = new NpgsqlCommand("select pg_advisory_unlock(9070095)", connection);
            await release.ExecuteScalarAsync(CancellationToken.None);
        }
    }
}

internal static class InteriorBusbarEcountRegistration
{
    internal static IServiceCollection AddInteriorBusbarEcount(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(InteriorBusbarEcountOptions.Load(configuration));
        services.AddSingleton<IInteriorBusbarEcountClient>(provider => new InteriorBusbarEcountClient(
            provider.GetRequiredService<InteriorBusbarEcountOptions>(), provider.GetRequiredService<TimeProvider>(),
            new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) },
            provider.GetRequiredService<ILogger<InteriorBusbarEcountClient>>()));
        services.AddHostedService<InteriorBusbarEcountWorker>();
        return services;
    }
}
