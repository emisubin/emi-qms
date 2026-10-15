using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class G2HomeProductionAverageTests
{
    // UTC Oct 11 is already Oct 12 in Seoul; today's partial production must be excluded.
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 11, 16, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task HomeAverageUsesRecordedHistoricalDaysIncludingZeroButNotMissingForecastOrToday()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(db.Configuration);
        await BusinessUnitIsolationTests.BootstrapTargetsAsync(new DatabaseRoleBootstrapper(db.Configuration, new DatabaseRuntimePrivilegeManager(), NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        await new DatabaseMigrationRunner(provider, DatabaseMigrationCatalog.FromPaths(Path.Combine(db.RepositoryRoot, "database", "migrations"), Path.Combine(db.RepositoryRoot, "database", "business-migrations")), new DatabaseRuntimePrivilegeManager(), db.Configuration, NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju, ct);
        var store = new G2OperationsStore(new CheongjuDatabase(provider), new Clock());
        var empty = await store.GetHomeAsync(null, null, ct);
        Assert.Null(empty.AverageDailyProduction);
        Assert.Equal(0, empty.ProductionSampleDays);
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Administrator, """
            insert into qms_users(id,development_user_key,display_name,department_id,is_active)
            values ('71000000-0000-0000-0000-000000000001','average-test','Synthetic average',(select id from departments where code='manufacturing'),true);
            insert into g2_daily_metrics(work_date,metric_code,quantity,is_forecast,created_by_user_id,updated_by_user_id)
            select work_date::date,code,quantity,forecast,'71000000-0000-0000-0000-000000000001','71000000-0000-0000-0000-000000000001'
            from (values
              ('2025-01-01','MorningProduction',10,false),
              ('2025-01-01','AfternoonProduction',20,false),
              ('2026-10-01','MorningProduction',0,false),
              ('2026-10-01','AfternoonProduction',null::integer,false),
              ('2026-10-02','MorningProduction',null::integer,false),
              ('2026-10-03','MorningProduction',900,true),
              ('2026-10-11','AfternoonProduction',15,false),
              ('2026-10-11','Delivery',999,false),
              ('2026-10-12','MorningProduction',800,false),
              ('2026-10-13','MorningProduction',700,false)
            ) seed(work_date,code,quantity,forecast);
            """, ct);
        var home = await store.GetHomeAsync(null, null, ct);
        Assert.Equal(new DateOnly(2026, 10, 12), home.Today);
        Assert.Equal(15m, home.AverageDailyProduction); // Three days: 30, 0, 15.
        Assert.Equal(3, home.ProductionSampleDays);
        // A selected display month must not trim the historical sample.
        var otherMonth = await store.GetHomeAsync(2026, 9, ct);
        Assert.Equal(home.AverageDailyProduction, otherMonth.AverageDailyProduction);
        Assert.Equal(home.ProductionSampleDays, otherMonth.ProductionSampleDays);
    }
}
