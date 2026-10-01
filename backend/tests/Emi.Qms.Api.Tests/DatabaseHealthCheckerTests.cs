using System.Diagnostics;
using Emi.Qms.Api.BusinessUnits;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class DatabaseHealthCheckerTests
{
    [Fact]
    public async Task SplitReadiness_CompletesWithinOneBudgetWhenOneTargetStalls()
    {
        var provider = CreateSplitProvider();
        var allProbesStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStalledProbe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stalledProbeFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedCount = 0;
        async Task<DatabaseHealthResult> Probe(BusinessUnitDatabaseTarget target, CancellationToken cancellationToken)
        {
            try
            {
                if (Interlocked.Increment(ref startedCount) == 3)
                {
                    allProbesStarted.TrySetResult();
                }

                await allProbesStarted.Task.WaitAsync(cancellationToken);
                if (target.Code == BusinessUnitCodes.Osan)
                {
                    await releaseStalledProbe.Task;
                }
                return new DatabaseHealthResult(true, "reachable");
            }
            finally
            {
                if (target.Code == BusinessUnitCodes.Osan) stalledProbeFinished.TrySetResult();
            }
        }

        var checker = new DatabaseHealthChecker(provider, Probe, TimeSpan.FromMilliseconds(150));
        var stopwatch = Stopwatch.StartNew();

        DatabaseHealthResult result;
        try
        {
            result = await checker.CheckAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseStalledProbe.TrySetResult();
        }
        await stalledProbeFinished.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsReady);
        Assert.Equal("business_unit_database_unready", result.Reason);
        Assert.True(result.BusinessUnits![BusinessUnitCodes.Cheongju]);
        Assert.False(result.BusinessUnits[BusinessUnitCodes.Osan]);
        Assert.Equal(3, startedCount);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SplitReadiness_PreservesCallerCancellation()
    {
        var provider = CreateSplitProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var checker = new DatabaseHealthChecker(
            provider,
            async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new DatabaseHealthResult(true, "reachable");
            },
            TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checker.CheckAsync(cancellation.Token));
    }

    private static DatabaseConnectionStringProvider CreateSplitProvider()
    {
        var values = new Dictionary<string, string?>
        {
            ["BusinessUnits:Enabled"] = "true"
        };
        foreach (var (section, code, version) in new[]
                 {
                     ("Directory", "DIRECTORY", BusinessUnitConfiguration.DirectorySchemaVersion),
                     ("Units:Cheongju", BusinessUnitCodes.Cheongju, BusinessUnitConfiguration.BusinessSchemaVersion),
                     ("Units:Osan", BusinessUnitCodes.Osan, BusinessUnitConfiguration.BusinessSchemaVersion)
                 })
        {
            var key = $"BusinessUnits:{section}";
            var token = code.ToLowerInvariant();
            values[$"{key}:Code"] = code;
            values[$"{key}:RuntimeConnection"] = $"{token}-runtime-connection";
            values[$"{key}:MigrationConnection"] = $"{token}-migration-connection";
            values[$"{key}:AdministratorConnection"] = $"{token}-admin-connection";
            values[$"{key}:ExpectedDatabaseName"] = $"{token}-database";
            values[$"{key}:MigrationRoleName"] = $"{token}-migrator";
            values[$"{key}:RuntimeRoleName"] = $"{token}-runtime";
            values[$"{key}:ExpectedSchemaVersion"] = version;
        }

        return new DatabaseConnectionStringProvider(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }
}
