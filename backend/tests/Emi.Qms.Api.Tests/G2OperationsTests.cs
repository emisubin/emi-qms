using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class G2OperationsTests(QmsWebApplicationFactory factory) : IClassFixture<QmsWebApplicationFactory>
{
    [Fact]
    public void InventoryCalculator_StartsAtFirstPhysicalCountAndPreservesNegativeValues()
    {
        var from = new DateOnly(2026, 8, 1);
        var result = G2InventoryCalculator.Calculate(
            from,
            from.AddDays(4),
            null,
            new Dictionary<DateOnly, int> { [from.AddDays(1)] = 5 },
            new Dictionary<DateOnly, long> { [from] = 20, [from.AddDays(2)] = 3 },
            new Dictionary<DateOnly, long> { [from.AddDays(2)] = 10, [from.AddDays(3)] = 2 },
            new Dictionary<DateOnly, long> { [from.AddDays(2)] = 1 });

        Assert.Null(result[from]);
        Assert.Equal(5, result[from.AddDays(1)]);
        Assert.Equal(-3, result[from.AddDays(2)]);
        Assert.Equal(-5, result[from.AddDays(3)]);
        Assert.Equal(-5, result[from.AddDays(4)]);
    }

    [Fact]
    public void InventoryCalculator_LaterPhysicalCountIsAnImmutableBoundaryForEarlierCorrections()
    {
        var from = new DateOnly(2026, 8, 1);
        var counts = new Dictionary<DateOnly, int> { [from] = 100, [from.AddDays(3)] = 80 };
        var before = G2InventoryCalculator.Calculate(from, from.AddDays(4), null, counts,
            new Dictionary<DateOnly, long> { [from.AddDays(1)] = 10, [from.AddDays(4)] = 5 },
            new Dictionary<DateOnly, long>(),
            new Dictionary<DateOnly, long>());
        var corrected = G2InventoryCalculator.Calculate(from, from.AddDays(4), null, counts,
            new Dictionary<DateOnly, long> { [from.AddDays(1)] = 25, [from.AddDays(4)] = 5 },
            new Dictionary<DateOnly, long>(),
            new Dictionary<DateOnly, long>());

        Assert.NotEqual(before[from.AddDays(2)], corrected[from.AddDays(2)]);
        Assert.Equal(before[from.AddDays(3)], corrected[from.AddDays(3)]);
        Assert.Equal(before[from.AddDays(4)], corrected[from.AddDays(4)]);
    }

    [Fact]
    public void InventoryCalculator_UsesPreviousDayProductionAndDefectsWithCurrentDayDeliveryFromCutover()
    {
        var from = G2InventoryCalculator.AvailableInventoryStartDate;
        var result = G2InventoryCalculator.Calculate(
            from,
            from.AddDays(1),
            3,
            new Dictionary<DateOnly, int>(),
            new Dictionary<DateOnly, long>
            {
                [from.AddDays(-1)] = 34,
                [from] = 47
            },
            new Dictionary<DateOnly, long>
            {
                [from.AddDays(-1)] = 99,
                [from] = 30,
                [from.AddDays(1)] = 40
            },
            new Dictionary<DateOnly, long>
            {
                [from.AddDays(-1)] = 1,
                [from] = 2
            });

        Assert.Equal(6, result[from]);
        Assert.Equal(11, result[from.AddDays(1)]);
    }

    [Fact]
    public void InventoryCalculator_PhysicalCountRemainsTheBoundaryForNextDayMovements()
    {
        var from = G2InventoryCalculator.AvailableInventoryStartDate;
        var result = G2InventoryCalculator.Calculate(
            from,
            from.AddDays(1),
            2,
            new Dictionary<DateOnly, int> { [from] = 10 },
            new Dictionary<DateOnly, long> { [from] = 47 },
            new Dictionary<DateOnly, long> { [from] = 30, [from.AddDays(1)] = 5 },
            new Dictionary<DateOnly, long> { [from] = 2 });

        Assert.Equal(10, result[from]);
        Assert.Equal(50, result[from.AddDays(1)]);
    }

    [Fact]
    public void InventoryCalculator_AddsPreviousDayRepairsOnlyFromSeptember10()
    {
        var from = new DateOnly(2026, 9, 9);
        var result = G2InventoryCalculator.Calculate(
            from,
            from.AddDays(1),
            20,
            new Dictionary<DateOnly, int>(),
            new Dictionary<DateOnly, long>
            {
                [from.AddDays(-1)] = 5,
                [from] = 4
            },
            new Dictionary<DateOnly, long>(),
            new Dictionary<DateOnly, long>
            {
                [from.AddDays(-1)] = 4,
                [from] = 1
            },
            new Dictionary<DateOnly, long>
            {
                [from.AddDays(-1)] = 1,
                [from] = 1
            });

        Assert.Equal(21, result[from]);
        Assert.Equal(25, result[from.AddDays(1)]);
    }

    [Fact]
    public void InventoryCalculator_DoesNotAddRepairsBeforeSeptember10AndPreservesPhysicalOverrides()
    {
        var from = G2InventoryCalculator.AvailableInventoryStartDate.AddDays(-2);
        var result = G2InventoryCalculator.Calculate(
            from,
            from.AddDays(2),
            10,
            new Dictionary<DateOnly, int> { [from.AddDays(1)] = 30 },
            new Dictionary<DateOnly, long> { [from] = 2, [from.AddDays(1)] = 5 },
            new Dictionary<DateOnly, long>(),
            new Dictionary<DateOnly, long> { [from] = 3, [from.AddDays(1)] = 4 },
            new Dictionary<DateOnly, long> { [from] = 1, [from.AddDays(1)] = 2 });

        Assert.Equal(9, result[from]);
        Assert.Equal(30, result[from.AddDays(1)]);
        Assert.Equal(31, result[from.AddDays(2)]);
    }

    [Fact]
    public void InventoryCalculator_UsesSameDayMovementsThroughAugust27()
    {
        var date = new DateOnly(2026, 8, 27);
        var result = G2InventoryCalculator.Calculate(
            date,
            date,
            20,
            new Dictionary<DateOnly, int>(),
            new Dictionary<DateOnly, long> { [date] = 7 },
            new Dictionary<DateOnly, long> { [date] = 4 },
            new Dictionary<DateOnly, long> { [date] = 3 },
            new Dictionary<DateOnly, long> { [date] = 99 });

        Assert.Equal(20, result[date]);
    }

    [Fact]
    public async Task DefectInventoryCountEndpoints_RequireInventoryManagePermission()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-viewer");

        using var save = await client.PutAsJsonAsync(
            "/api/g2/defect-inventory-counts/2026-08-18",
            new { quantity = 0, expectedVersion = (int?)null },
            TestContext.Current.CancellationToken);
        using var delete = await client.DeleteAsync(
            "/api/g2/defect-inventory-counts/2026-08-18?expectedVersion=1",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Theory]
    [InlineData("dev-admin", true, true, true, true, true, true, true, true, true)]
    [InlineData("dev-sales", false, true, true, true, true, true, true, true, true)]
    [InlineData("dev-manufacturing", true, false, true, true, true, true, false, true, true)]
    [InlineData("dev-logistics", false, false, false, false, false, false, false, false, false)]
    [InlineData("dev-quality", true, false, false, true, false, false, false, false, true)]
    [InlineData("dev-viewer", false, false, false, false, false, false, false, false, false)]
    public async Task G2Policies_UseTheApprovedRoleMatrix(
        string userKey,
        bool production,
        bool delivery,
        bool attendance,
        bool inventory,
        bool target,
        bool productionPlan,
        bool deliveryPlan,
        bool planNote,
        bool note)
    {
        var profile = await new InMemoryIdentityStore().GetProfileByDevelopmentUserKeyAsync(userKey, TestContext.Current.CancellationToken);
        Assert.NotNull(profile);
        var claims = new List<Claim>
        {
            new(QmsClaimTypes.UserId, profile.User.Id.ToString("D")),
            new(ClaimTypes.NameIdentifier, profile.User.Id.ToString("D"))
        };
        claims.AddRange(profile.Roles.Select(role => new Claim(ClaimTypes.Role, role.Code)));
        claims.AddRange(profile.Permissions.Select(permission => new Claim(QmsClaimTypes.Permission, permission.Code)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, DevelopmentAuthenticationDefaults.Scheme));
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();

        Assert.True((await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2Read)).Succeeded);
        Assert.Equal(production, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2ProductionUpdate)).Succeeded);
        Assert.Equal(delivery, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2DeliveryUpdate)).Succeeded);
        Assert.Equal(attendance, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2AttendanceUpdate)).Succeeded);
        Assert.Equal(inventory, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2InventoryManage)).Succeeded);
        Assert.Equal(target, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2TargetManage)).Succeeded);
        Assert.Equal(productionPlan, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2ProductionPlanUpdate)).Succeeded);
        Assert.Equal(deliveryPlan, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2DeliveryPlanUpdate)).Succeeded);
        Assert.Equal(planNote, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2PlanNoteUpdate)).Succeeded);
        Assert.Equal(note, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2NoteUpdate)).Succeeded);
    }

    [Fact]
    public async Task OperationsEndpoint_RejectsEntireMixedPermissionRequestBeforeDatabaseWrite()
    {
        await using var databases = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(TestContext.Current.CancellationToken);
        using var databaseFactory = await CreateOperationsFactoryAsync(databases);
        using var client = databaseFactory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-manufacturing");
        using var response = await client.PutAsJsonAsync(
            "/api/g2/operations/2026-08-18",
            new
            {
                morningProduction = new { quantity = 10, expectedVersion = (int?)null },
                delivery = new { quantity = 4, expectedVersion = (int?)null }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator, "select count(*) from g2_daily_metrics",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OperationsEndpoint_RejectsDefectForLogisticsRole()
    {
        await using var databases = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(TestContext.Current.CancellationToken);
        using var databaseFactory = await CreateOperationsFactoryAsync(databases);
        using var client = databaseFactory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-logistics");
        using var response = await client.PutAsJsonAsync(
            "/api/g2/operations/2026-08-18",
            new { defect = new { quantity = 2, expectedVersion = (int?)null } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator, "select count(*) from g2_daily_metrics",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OperationsEndpoint_RejectsRepairsForLogisticsRole()
    {
        await using var databases = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(TestContext.Current.CancellationToken);
        using var databaseFactory = await CreateOperationsFactoryAsync(databases);
        using var client = databaseFactory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-logistics");
        using var response = await client.PutAsJsonAsync(
            "/api/g2/operations/2026-08-18",
            new { morningRepair = new { quantity = 1, expectedVersion = (int?)null } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator, "select count(*) from g2_daily_metrics",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WorkspaceEndpoint_SavesAtomicallySupportsTombstonesAndRejectsFutureActuals()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        using var databaseFactory = await CreateOperationsFactoryAsync(databases);
        using var client = databaseFactory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-admin");
        const string date = "2026-09-22";

        using var saved = await client.PutAsJsonAsync($"/api/g2/workspace/{date}", new
        {
            productionPlan = new { quantity = 20, expectedVersion = (int?)null },
            deliveryPlan = new { quantity = 10, expectedVersion = (int?)null },
            morningProduction = new { quantity = 5, expectedVersion = (int?)null },
            afternoonProduction = new { quantity = 3, expectedVersion = (int?)null },
            physicalCount = new { quantity = 100, expectedVersion = (int?)null },
            planNote = new { text = "계획", expectedVersion = (int?)null },
            actualNote = new { text = "공유 실적", expectedVersion = (int?)null },
            rowNote = new { text = "행 메모", expectedVersion = (int?)null }
        }, ct);
        Assert.True(saved.IsSuccessStatusCode, FailureDetails(databaseFactory, await saved.Content.ReadAsStringAsync(ct)));

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/g2/days?from={date}&to={date}", ct);
        var day = first.GetProperty("days")[0];
        Assert.Equal(20, day.GetProperty("productionPlan").GetProperty("quantity").GetInt32());
        Assert.Equal(JsonValueKind.Null, day.GetProperty("defectInventoryCount").ValueKind);
        Assert.Equal("공유 실적", day.GetProperty("actualNote").GetProperty("text").GetString());
        Assert.Equal(8, day.GetProperty("productionTotal").GetInt64());
        Assert.True(await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator,
            "select count(*) from audit_event_changes where target_type in ('g2_daily_values','g2_daily_notes');", ct) > 0);
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator,
            "select count(*) from audit_event_changes where target_type='g2_daily_notes' and after_value in ('계획','공유 실적','행 메모');", ct));

        using var conflict = await client.PutAsJsonAsync($"/api/g2/workspace/{date}", new
        {
            productionPlan = new { quantity = 21, expectedVersion = 1 },
            actualNote = new { text = "오래된 메모", expectedVersion = 999 }
        }, ct);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var afterRollback = await client.GetFromJsonAsync<JsonElement>($"/api/g2/days?from={date}&to={date}", ct);
        day = afterRollback.GetProperty("days")[0];
        Assert.Equal(20, day.GetProperty("productionPlan").GetProperty("quantity").GetInt32());
        Assert.Equal(1, day.GetProperty("productionPlan").GetProperty("version").GetInt32());

        using var cleared = await client.PutAsJsonAsync($"/api/g2/workspace/{date}", new
        {
            productionPlan = new { quantity = (int?)null, expectedVersion = 1 },
            physicalCount = new { quantity = (int?)null, expectedVersion = 1 },
            actualNote = new { text = (string?)null, expectedVersion = 1 }
        }, ct);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var afterClear = await client.GetFromJsonAsync<JsonElement>($"/api/g2/days?from={date}&to={date}", ct);
        day = afterClear.GetProperty("days")[0];
        Assert.Equal(JsonValueKind.Null, day.GetProperty("productionPlan").GetProperty("quantity").ValueKind);
        Assert.Equal(2, day.GetProperty("productionPlan").GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKind.Null, day.GetProperty("physicalCount").GetProperty("quantity").ValueKind);
        Assert.Equal(JsonValueKind.Null, day.GetProperty("actualNote").GetProperty("text").ValueKind);

        var concurrentA = client.PutAsJsonAsync($"/api/g2/workspace/{date}", new
        {
            actualNote = new { text = "동시 메모 A", expectedVersion = 2 }
        }, ct);
        var concurrentB = client.PutAsJsonAsync($"/api/g2/workspace/{date}", new
        {
            actualNote = new { text = "동시 메모 B", expectedVersion = 2 }
        }, ct);
        using var concurrentResponseA = await concurrentA;
        using var concurrentResponseB = await concurrentB;
        var concurrentStatuses = new[] { concurrentResponseA.StatusCode, concurrentResponseB.StatusCode };
        Assert.Single(concurrentStatuses, status => status == HttpStatusCode.OK);
        Assert.Single(concurrentStatuses, status => status == HttpStatusCode.Conflict);
        var afterConcurrent = await client.GetFromJsonAsync<JsonElement>($"/api/g2/days?from={date}&to={date}", ct);
        day = afterConcurrent.GetProperty("days")[0];
        Assert.Equal(3, day.GetProperty("actualNote").GetProperty("version").GetInt32());
        Assert.Contains(day.GetProperty("actualNote").GetProperty("text").GetString(), new[] { "동시 메모 A", "동시 메모 B" });

        using var future = await client.PutAsJsonAsync("/api/g2/workspace/2099-01-02", new
        {
            productionPlan = new { quantity = 7, expectedVersion = (int?)null },
            morningProduction = new { quantity = 1, expectedVersion = (int?)null }
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator,
            "select count(*) from g2_daily_values where work_date='2099-01-02';", ct));

        using var futurePlan = await client.PutAsJsonAsync("/api/g2/workspace/2099-01-02", new
        {
            productionPlan = new { quantity = 7, expectedVersion = (int?)null },
            actualNote = new { text = "미래 메모", expectedVersion = (int?)null }
        }, ct);
        Assert.Equal(HttpStatusCode.OK, futurePlan.StatusCode);
        var futureDay = await client.GetFromJsonAsync<JsonElement>("/api/g2/days?from=2099-01-02&to=2099-01-02", ct);
        day = futureDay.GetProperty("days")[0];
        Assert.Equal(7, day.GetProperty("productionPlan").GetProperty("quantity").GetInt32());
        Assert.Equal("미래 메모", day.GetProperty("actualNote").GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, day.GetProperty("productionTotal").ValueKind);
    }

    [Fact]
    public async Task LegacyInventoryDeletes_PreserveVersionsAndRejectStaleWorkspaceWrites()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        using var databaseFactory = await CreateOperationsFactoryAsync(databases);
        using var client = databaseFactory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-admin");
        const string date = "2026-09-24";

        using var created = await client.PutAsJsonAsync($"/api/g2/workspace/{date}", new
        {
            physicalCount = new { quantity = 20, expectedVersion = (int?)null }
        }, ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var initial = await client.GetFromJsonAsync<JsonElement>($"/api/g2/days?from={date}&to={date}", ct);
        var day = initial.GetProperty("days")[0];
        Assert.Equal(1, day.GetProperty("physicalCount").GetProperty("version").GetInt32());

        client.DefaultRequestHeaders.Remove(DevelopmentAuthenticationDefaults.UserHeader);
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-sales");
        using var deletedPhysical = await client.DeleteAsync($"/api/g2/inventory-counts/{date}?expectedVersion=1", ct);
        using var deletedDefect = await client.DeleteAsync($"/api/g2/defect-inventory-counts/{date}?expectedVersion=1", ct);
        Assert.Equal(HttpStatusCode.OK, deletedPhysical.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, deletedDefect.StatusCode);

        var deleted = await client.GetFromJsonAsync<JsonElement>($"/api/g2/days?from={date}&to={date}", ct);
        day = deleted.GetProperty("days")[0];
        Assert.Equal(JsonValueKind.Null, day.GetProperty("physicalCount").GetProperty("quantity").ValueKind);
        Assert.Equal(2, day.GetProperty("physicalCount").GetProperty("version").GetInt32());
        Assert.Equal("G2 Test Sales", day.GetProperty("physicalCount").GetProperty("updatedByDisplayName").GetString());

        using var recreatedPhysical = await client.PutAsJsonAsync($"/api/g2/inventory-counts/{date}", new
        {
            quantity = 30,
            expectedVersion = 2
        }, ct);
        using var recreatedDefect = await client.PutAsJsonAsync($"/api/g2/defect-inventory-counts/{date}", new
        {
            quantity = 5,
            expectedVersion = 2
        }, ct);
        Assert.Equal(HttpStatusCode.OK, recreatedPhysical.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, recreatedDefect.StatusCode);

        using var stalePhysical = await client.PutAsJsonAsync($"/api/g2/workspace/{date}", new
        {
            physicalCount = new { quantity = 99, expectedVersion = 1 }
        }, ct);
        using var staleDefect = await client.PutAsJsonAsync($"/api/g2/workspace/{date}", new
        {
            defectInventoryCount = new { quantity = 99, expectedVersion = 1 }
        }, ct);
        Assert.Equal(HttpStatusCode.Conflict, stalePhysical.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, staleDefect.StatusCode);

        var final = await client.GetFromJsonAsync<JsonElement>($"/api/g2/days?from={date}&to={date}", ct);
        day = final.GetProperty("days")[0];
        Assert.Equal(30, day.GetProperty("physicalCount").GetProperty("quantity").GetInt32());
        Assert.Equal(3, day.GetProperty("physicalCount").GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task WorkspaceEndpoint_RejectsMixedPlanPermissionsBeforeWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        using var databaseFactory = await CreateOperationsFactoryAsync(databases);
        using var client = databaseFactory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-manufacturing");
        var assignments = await databases.ReadColumnAsync(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator, """
            select role.code || '|' || permission.code
            from role_permissions assignment
            join roles role on role.id=assignment.role_id
            join permissions permission on permission.id=assignment.permission_id
            where permission.code in (
              'G2.Production.Update','G2.Delivery.Update','G2.Inventory.Manage',
              'G2.ProductionPlan.Update','G2.DeliveryPlan.Update','G2.PlanNote.Update','G2.Note.Update')
            order by (role.code || '|' || permission.code) collate "C";
            """, ct);
        Assert.Equal(new[]
        {
            "manufacturing|G2.Inventory.Manage", "manufacturing|G2.Note.Update",
            "manufacturing|G2.PlanNote.Update", "manufacturing|G2.Production.Update",
            "manufacturing|G2.ProductionPlan.Update", "quality|G2.Inventory.Manage",
            "quality|G2.Note.Update", "quality|G2.Production.Update",
            "sales|G2.Delivery.Update", "sales|G2.DeliveryPlan.Update",
            "sales|G2.Inventory.Manage", "sales|G2.Note.Update", "sales|G2.PlanNote.Update",
            "sales|G2.ProductionPlan.Update", "system-administrator|G2.Delivery.Update",
            "system-administrator|G2.DeliveryPlan.Update", "system-administrator|G2.Inventory.Manage",
            "system-administrator|G2.Note.Update", "system-administrator|G2.PlanNote.Update",
            "system-administrator|G2.Production.Update", "system-administrator|G2.ProductionPlan.Update"
        }, assignments);
        using var response = await client.PutAsJsonAsync("/api/g2/workspace/2026-09-23", new
        {
            productionPlan = new { quantity = 5, expectedVersion = (int?)null },
            deliveryPlan = new { quantity = 4, expectedVersion = (int?)null }
        }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator,
            "select count(*) from g2_daily_values where work_date='2026-09-23';", ct));
    }

    [Fact]
    public async Task PlansAndForecasts_UsePerDayOverwriteRulesAndNeverPromoteForecastActuals()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        using var databaseFactory = await CreateOperationsFactoryAsync(databases);
        using var client = databaseFactory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-sales");

        using var bulk = await client.PutAsJsonAsync("/api/g2/plans", new
        {
            from = "2026-09-20", to = "2026-09-21", excludeSundays = true, overwrite = false,
            productionPlan = 5, deliveryPlan = 0, planNote = (string?)null
        }, ct);
        Assert.True(bulk.IsSuccessStatusCode, FailureDetails(databaseFactory, await bulk.Content.ReadAsStringAsync(ct)));
        using var skipped = await client.PutAsJsonAsync("/api/g2/plans", new
        {
            from = "2026-09-21", to = "2026-09-21", excludeSundays = false, overwrite = false,
            productionPlan = 9
        }, ct);
        Assert.Equal(HttpStatusCode.OK, skipped.StatusCode);
        var range = await client.GetFromJsonAsync<JsonElement>("/api/g2/days?from=2026-09-20&to=2026-09-21", ct);
        Assert.Equal(JsonValueKind.Null, range.GetProperty("days")[0].GetProperty("productionPlan").ValueKind);
        Assert.Equal(5, range.GetProperty("days")[1].GetProperty("productionPlan").GetProperty("quantity").GetInt32());
        Assert.Equal(0, range.GetProperty("days")[1].GetProperty("deliveryPlan").GetProperty("quantity").GetInt32());
        Assert.Equal(JsonValueKind.Null, range.GetProperty("days")[1].GetProperty("planNote").GetProperty("text").ValueKind);

        using var cleared = await client.PutAsJsonAsync("/api/g2/workspace/2026-09-21", new
        {
            productionPlan = new { quantity = (int?)null, expectedVersion = 1 }
        }, ct);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        using var filledAfterClear = await client.PutAsJsonAsync("/api/g2/plans", new
        {
            from = "2026-09-21", to = "2026-09-21", excludeSundays = false, overwrite = false,
            productionPlan = 9, deliveryPlan = 7, planNote = "재입력 계획"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, filledAfterClear.StatusCode);
        range = await client.GetFromJsonAsync<JsonElement>("/api/g2/days?from=2026-09-21&to=2026-09-21", ct);
        Assert.Equal(9, range.GetProperty("days")[0].GetProperty("productionPlan").GetProperty("quantity").GetInt32());
        Assert.Equal(0, range.GetProperty("days")[0].GetProperty("deliveryPlan").GetProperty("quantity").GetInt32());
        Assert.Equal("재입력 계획", range.GetProperty("days")[0].GetProperty("planNote").GetProperty("text").GetString());

        using var overwritten = await client.PutAsJsonAsync("/api/g2/plans", new
        {
            from = "2026-09-21", to = "2026-09-21", excludeSundays = false, overwrite = true,
            productionPlan = 11
        }, ct);
        Assert.Equal(HttpStatusCode.OK, overwritten.StatusCode);
        range = await client.GetFromJsonAsync<JsonElement>("/api/g2/days?from=2026-09-21&to=2026-09-21", ct);
        Assert.Equal(11, range.GetProperty("days")[0].GetProperty("productionPlan").GetProperty("quantity").GetInt32());

        await databases.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Administrator, """
            insert into g2_daily_metrics(work_date,metric_code,quantity,is_forecast,created_by_user_id,updated_by_user_id)
            values ('2026-09-19','MorningProduction',44,true,
                '50000000-0000-0000-0000-000000000004','50000000-0000-0000-0000-000000000004');
            """, ct);
        var hidden = await client.GetFromJsonAsync<JsonElement>("/api/g2/days?from=2026-09-19&to=2026-09-19", ct);
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("days")[0].GetProperty("morningProduction").ValueKind);
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("days")[0].GetProperty("productionTotal").ValueKind);

        client.DefaultRequestHeaders.Remove(DevelopmentAuthenticationDefaults.UserHeader);
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-manufacturing");
        using var actual = await client.PutAsJsonAsync("/api/g2/workspace/2026-09-19", new
        {
            morningProduction = new { quantity = 7, expectedVersion = (int?)null }
        }, ct);
        Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        Assert.Equal(2L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Administrator,
            "select count(*) from g2_daily_metrics where work_date='2026-09-19' and metric_code='MorningProduction';", ct));
        var visible = await client.GetFromJsonAsync<JsonElement>("/api/g2/days?from=2026-09-19&to=2026-09-19", ct);
        Assert.Equal(7, visible.GetProperty("days")[0].GetProperty("morningProduction").GetProperty("quantity").GetInt32());
    }

    private static async Task<QmsWebApplicationFactory> CreateOperationsFactoryAsync(
        BusinessUnitIsolationTests.IsolationDatabaseSet databases)
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        await new DatabaseRoleBootstrapper(databases.Configuration, new DatabaseRuntimePrivilegeManager(),
            NullLogger<DatabaseRoleBootstrapper>.Instance)
            .BootstrapAsync(BusinessUnitCodes.Cheongju, ct);
        await new DatabaseMigrationRunner(provider,
            DatabaseMigrationCatalog.FromPaths(
                Path.Combine(databases.RepositoryRoot, "database", "migrations"),
                Path.Combine(databases.RepositoryRoot, "database", "business-migrations")),
            new DatabaseRuntimePrivilegeManager(), databases.Configuration,
            NullLogger<DatabaseMigrationRunner>.Instance)
            .ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju, ct);
        await databases.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Administrator, """
            insert into qms_users(id,development_user_key,display_name,department_id,is_active)
            values
              ('50000000-0000-0000-0000-000000000001','g2-test-admin','G2 Test Admin',(select id from departments where code='administration'),true),
              ('50000000-0000-0000-0000-000000000002','g2-test-sales','G2 Test Sales',(select id from departments where code='sales'),true),
              ('50000000-0000-0000-0000-000000000004','g2-test-manufacturing','G2 Test Manufacturing',(select id from departments where code='manufacturing'),true)
            on conflict (id) do nothing;
            """, ct);
        // The real maintenance middleware runs before the endpoint's per-field permission checks.
        return QmsWebApplicationFactory.Create("Testing", new Dictionary<string, string?>
        {
            ["DevAuthentication:Enabled"] = "true",
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["DevelopmentData:SeedEnabled"] = "false",
            ["ConnectionStrings:QmsDatabase"] = provider.GetConnectionString(
                databases.BusinessUnits.GetBusiness(BusinessUnitCodes.Cheongju))
        });
    }

    private static string FailureDetails(QmsWebApplicationFactory factory, string response) =>
        response + Environment.NewLine + string.Join(Environment.NewLine,
            factory.Logs.Entries.Select(entry => $"{entry.Message}{Environment.NewLine}{entry.Exception}"));
}
