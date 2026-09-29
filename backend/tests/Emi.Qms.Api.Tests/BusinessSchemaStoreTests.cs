using Emi.Qms.Api.Audit;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class BusinessSchemaSqlTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WebPush_sql_only_mentions_tables_and_columns_owned_by_its_business(bool osan)
    {
        var sql = NotificationDeliveryStore.BuildWebPushSql(osan);
        Assert.DoesNotContain("project_profile", sql);
        if (osan)
        {
            Assert.DoesNotContain("work_items", sql);
            Assert.DoesNotContain("n.work_item_id", sql);
            Assert.DoesNotContain("eligible.work_item_id", sql);
            Assert.Contains("osan_notification_global_preferences", sql);
            Assert.Contains("preference.stage_sequence=osan_event.stage_sequence", sql);
        }
        else
        {
            Assert.DoesNotContain("osan_notification_", sql);
            Assert.Contains("left join work_items", sql);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Delivery_reads_keep_snapshot_and_claim_fields_without_missing_joins(bool osan)
    {
        var sql = NotificationDeliveryStore.BuildDeliveryReadSql(osan, "where nd.id = @id");
        Assert.Contains("nd.manual_payload_json::text", sql);
        Assert.Contains("nd.claim_token", sql);
        Assert.Contains("where nd.id = @id", sql);
        if (osan)
        {
            Assert.DoesNotContain("work_items", sql);
            Assert.DoesNotContain("workflow_stages", sql);
            Assert.DoesNotContain("nd.work_item_id", sql);
            Assert.Contains("null::uuid", sql);
        }
        else Assert.Contains("nd.work_item_id", sql);
    }
}

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task BusinessSchemaSeparation_StoresExecuteAgainstTheirReducedSchemas()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(databases.RepositoryRoot));
        foreach (var code in new[] { "DIRECTORY", BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
            await new DatabaseRoleBootstrapper(databases.Configuration, new DatabaseRuntimePrivilegeManager(),
                NullLogger<DatabaseRoleBootstrapper>.Instance).BootstrapAsync(code, ct);
        var runner = new DatabaseMigrationRunner(provider, catalog, new DatabaseRuntimePrivilegeManager(),
            databases.Configuration, NullLogger<DatabaseMigrationRunner>.Instance);
        foreach (var code in new[] { "DIRECTORY", BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
            await runner.ApplyAndVerifyAsync(code, ct);

        await AssertApprovedSchemaAsync(databases, ct);
        await AssertRuntimeRoleBoundariesAsync(databases);
        var actor = Guid.NewGuid();
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into qms_users(id,development_user_key,display_name,is_active)
            values('{actor:D}','schema-store-test','Synthetic Schema User',true);
            """, ct);
        var osan = new OsanDatabase(provider);
        var policy = new OsanPolicyStore(osan);
        var customer = Assert.IsType<OsanCustomer>((await policy.CreateCustomerAsync("Synthetic Schema Customer", ct)).Value);
        var assignment = Assert.Single(await policy.AssignmentUsersAsync(ct), user => user.UserId == actor);
        Assert.Equal(200, (await policy.AssignAsync(actor, [customer.CustomerId], assignment.Version, ct)).Status);
        var projects = new OsanProjectStore(osan);
        var input = new NormalizedCreateOsanProjectInput("Duplicated title", "DUPLICATED-CODE", customer.Name,
            "PO-1", "WO-1", new DateOnly(2026, 10, 1), "Rack", 1, Guid.NewGuid(), customer.CustomerId);
        var first = await projects.CreateAsync(input, actor, ct);
        // Manual registration retains its existing duplicate-code rejection;
        // imported duplicate codes are covered by the upgrade preservation test.
        var second = await projects.CreateAsync(input with { ProjectCode = "SECOND-CODE", OperationId = Guid.NewGuid() }, actor, ct);
        Assert.Equal(OsanProjectCreateStatus.Success, first.Status);
        Assert.Equal(OsanProjectCreateStatus.Success, second.Status);
        var projectId = first.Value!.Project.ProjectId;
        Assert.Equal(2, (await projects.ListAsync(new ProjectAccessScope(true, []), ct)).Items.Count);

        await new OsanDashboardStore(osan, TimeProvider.System).GetAsync(
            new OsanDashboardQuery("", "", 1, 20), new ProjectAccessScope(true, []), ct);
        await new OsanPersonalHomeStore(osan, TimeProvider.System).GetAsync(actor, new ProjectAccessScope(true, []), ct);
        var identity = new DbIdentityStore(osan, databases.Configuration);
        var key = $"osan-{projectId:N}";
        var authorized = await identity.GetProjectByKeyAsync(key, ct);
        Assert.Equal(input.ProjectCode, authorized!.ProjectNumber);
        Assert.Equal(input.Title, authorized.Name);
        var profile = await identity.GetProfileByUserIdAsync(actor, ct);
        Assert.Equal(2, profile!.ProjectAccess.Count);
        Assert.All(profile.ProjectAccess, item => Assert.Equal(input.Title, item.Name));

        var progress = new OsanProgressStore(osan);
        for (var stage = 1; stage <= 7; stage++)
        {
            var target = (await progress.GetAsync(projectId, ct))!.Targets[0];
            var completed = await progress.CompleteAsync(projectId,
                new CompleteOsanProgressInput(Guid.NewGuid(), "individual", stage,
                    [new(target.TargetId, target.Version)], [], "Synthetic completion"), actor, ct, true);
            Assert.Equal(OsanProgressMutationStatus.Success, completed.Status);
        }
        var finished = (await progress.GetAsync(projectId, ct))!;
        Assert.Equal("Completed", finished.Status);
        Assert.All(finished.Targets[0].Steps, step => Assert.Equal(actor, step.CompletedByUserId));

        var workflow = new WorkflowStore(osan);
        var notifications = await workflow.GetNotificationsAsync(actor, null, ct);
        Assert.NotEmpty(notifications.Items);
        Assert.All(notifications.Items, notification =>
        {
            Assert.Equal("", notification.ProjectItem);
            Assert.Null(notification.WorkItemId);
        });
        var notificationId = notifications.Items[0].NotificationId;
        Assert.NotNull((await workflow.GetNotificationDetailAsync(notificationId, actor, false, ct)).Value);
        Assert.NotNull((await workflow.MarkNotificationReadAsync(notificationId, actor, ct)).Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.ListStagesAsync(ct));

        var delivery = new NotificationDeliveryStore(osan, TimeProvider.System, databases.Configuration);
        var rows = await delivery.ListDeliveriesAsync(null, null, null, null, ct);
        Assert.NotEmpty(rows.Items);
        Assert.NotNull(await delivery.GetDeliveryDetailAsync(rows.Items[0].DeliveryId, ct));
        var claimed = await delivery.ClaimDueDeliveriesAsync(5, 3, "schema-test", TimeSpan.FromMinutes(1), ct);
        Assert.NotEmpty(claimed);
        Assert.All(claimed, claim => Assert.NotNull(claim.Delivery.ManualPayloadJson));
        await delivery.CreateImmediateDeliveriesAsync(new NotificationOptions(), ct);
        var audit = new AuditStore(osan, TimeProvider.System, NullLogger<AuditStore>.Instance);
        var now = DateTimeOffset.UtcNow;
        await audit.ListAsync(DateOnly.FromDateTime(now.UtcDateTime.AddDays(-1)), DateOnly.FromDateTime(now.UtcDateTime),
            new AuditQuery(now.AddDays(-1), now.AddDays(1), null, null, null, null, null, null, 1, 50), ct);

        var cheongju = new CheongjuDatabase(provider);
        var cheongjuDelivery = new NotificationDeliveryStore(cheongju, TimeProvider.System, databases.Configuration);
        await cheongjuDelivery.CreateImmediateDeliveriesAsync(new NotificationOptions(), ct);
        Assert.Empty((await cheongjuDelivery.ListDeliveriesAsync(null, null, null, null, ct)).Items);
    }
}
