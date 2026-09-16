using System.Net;
using System.Net.Http.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task OsanNotificationPreferences_AreSelfOnlyIsolatedAndDefaultOn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var environment = new TestEnvironment(databases.RepositoryRoot);
        var catalog = new DatabaseMigrationCatalog(environment);
        await new DatabaseRoleBootstrapper(databases.Configuration, new DatabaseRuntimePrivilegeManager(),
            NullLogger<DatabaseRoleBootstrapper>.Instance).BootstrapAsync(ct);
        await new DatabaseMigrationRunner(provider, catalog, new DatabaseRuntimePrivilegeManager(),
            databases.Configuration, NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(ct);
        await new DevelopmentIdentitySeeder(provider, databases.Configuration, environment,
            NullLogger<DevelopmentIdentitySeeder>.Instance, new MigrationLedgerInspector(catalog)).SeedAsync(ct);
        databases.ConfigurationValues["DevelopmentData:SeedEnabled"] = "false";
        await databases.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            insert into directory_identities(user_id,auth_provider,external_subject,display_name,is_active)
            values('{SalesUserId}','Dev','dev-sales','Sales',true),('50000000-0000-0000-0000-000000000005','Dev','dev-quality','Quality',true)
            on conflict(user_id) do nothing;
            insert into directory_business_unit_memberships(user_id,business_unit_code,is_active)
            values('{SalesUserId}','OSAN',true),('50000000-0000-0000-0000-000000000005','OSAN',true)
            on conflict(user_id,business_unit_code) do update set is_active=true;
            """, ct);
        using var factory = QmsWebApplicationFactory.Create(DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues, includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();

        async Task<HttpResponseMessage> Send(HttpMethod method, string user, string unit, object? body = null)
        {
            using var request = Request(method, "/api/osan/my/notification-preferences", user, unit);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request, ct);
        }

        OsanNotificationPreferenceResponse defaults;
        using (var response = await Send(HttpMethod.Get, "dev-sales", BusinessUnitCodes.Osan))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            defaults = (await response.Content.ReadFromJsonAsync<OsanNotificationPreferenceResponse>(ct))!;
            Assert.Equal(7, defaults.Items.Count);
            Assert.Equal(7, defaults.StepCompletedStages.Count);
            Assert.All(defaults.Items, item => { Assert.True(item.MailEnabled); Assert.True(item.PushEnabled); });
        }

        foreach (var nullStage in new[] { false, true })
        {
            var malformedItems = defaults.Items.Cast<object?>().ToArray();
            var malformedStages = defaults.StepCompletedStages.Cast<object?>().ToArray();
            if (nullStage) malformedStages[0] = null;
            else malformedItems[0] = null;
            using var invalid = await Send(HttpMethod.Put, "dev-sales", BusinessUnitCodes.Osan,
                new { expectedVersion = defaults.Version, items = malformedItems, stepCompletedStages = malformedStages });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        var updatedItems = defaults.Items.Select(item => item.Kind == "StepCompleted"
            ? item with { MailEnabled = false }
            : item).ToArray();
        var updatedStages = defaults.StepCompletedStages.Select(stage => stage.Sequence == 3
            ? stage with { PushEnabled = false }
            : stage).ToArray();
        using (var response = await Send(HttpMethod.Put, "dev-sales", BusinessUnitCodes.Osan,
                   new UpdateOsanNotificationPreferencesRequest(defaults.Version, updatedItems, updatedStages)))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = (await response.Content.ReadFromJsonAsync<OsanNotificationPreferenceResponse>(ct))!;
            Assert.False(saved.Items.Single(item => item.Kind == "StepCompleted").MailEnabled);
            Assert.False(saved.StepCompletedStages.Single(stage => stage.Sequence == 3).PushEnabled);
        }

        Assert.Equal(2L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(distinct target_type) from audit_event_changes where target_type in ('osan_notification_preference_profiles','osan_notification_preferences');", ct));
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from audit_event_changes where target_type in ('osan_notification_preference_profiles','osan_notification_preferences');", ct));

        using (var other = await Send(HttpMethod.Get, "dev-quality", BusinessUnitCodes.Osan))
        {
            Assert.True(other.StatusCode == HttpStatusCode.OK,
                $"Expected second user's Osan preferences, got {(int)other.StatusCode}: {await other.Content.ReadAsStringAsync(ct)}");
            var own = (await other.Content.ReadFromJsonAsync<OsanNotificationPreferenceResponse>(ct))!;
            Assert.All(own.Items, item => { Assert.True(item.MailEnabled); Assert.True(item.PushEnabled); });
        }
        using (var wrongUnit = await Send(HttpMethod.Get, "dev-quality", BusinessUnitCodes.Cheongju))
            Assert.Equal(HttpStatusCode.Forbidden, wrongUnit.StatusCode);
        using (var conflict = await Send(HttpMethod.Put, "dev-sales", BusinessUnitCodes.Osan,
                   new UpdateOsanNotificationPreferencesRequest(defaults.Version, updatedItems, updatedStages)))
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        Assert.Equal(2L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration, "select count(*) from osan_notification_preferences;", ct));
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration, "select count(*) from osan_notification_preferences;", ct));
    }

    [Fact]
    public async Task OsanNotificationPreferences_SuppressWriterAndPushCreationAndRecheckQueuedDeliveries()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var environment = new TestEnvironment(databases.RepositoryRoot);
        var catalog = new DatabaseMigrationCatalog(environment);
        await new DatabaseRoleBootstrapper(databases.Configuration, new DatabaseRuntimePrivilegeManager(),
            NullLogger<DatabaseRoleBootstrapper>.Instance).BootstrapAsync(ct);
        await new DatabaseMigrationRunner(provider, catalog, new DatabaseRuntimePrivilegeManager(),
            databases.Configuration, NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(ct);
        await new DevelopmentIdentitySeeder(provider, databases.Configuration, environment,
            NullLogger<DevelopmentIdentitySeeder>.Instance, new MigrationLedgerInspector(catalog)).SeedAsync(ct);

        var projectId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into projects(id,project_key,project_number,name,customer_name,item,project_code,project_title,
                project_title_normalized,delivery_date,sales_owner_user_id,status,created_by_user_id,
                project_profile,osan_product_name,osan_quantity)
            values('{projectId}','prefs-project','PREFS-PROJECT','Prefs Project','Synthetic Customer','UL891',
                'PREFS-PROJECT','Prefs Project','PREFS PROJECT',current_date+30,'{SalesUserId}','Active',
                '{SalesUserId}','Osan','Synthetic Product',1);
            insert into web_push_subscriptions(id,user_id,endpoint,endpoint_hash,p256dh_key,auth_key,activated_at_utc)
            values('{subscriptionId}','{SalesUserId}','https://push.example.test/prefs','prefs-hash','p256dh','auth',now()-interval '1 day');
            insert into osan_notification_preferences(user_id,event_kind,channel,stage_sequence,is_enabled)
            values('{SalesUserId}','ProjectCreated','Mail',0,false),
                  ('{SalesUserId}','ProjectCreated','WebPush',0,false);
            """, ct);

        var osan = databases.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan);
        var requestContext = new DefaultHttpContext();
        BusinessUnitRequestContextFeature.Set(requestContext, new BusinessUnitRequestContext(
            BusinessUnitAccessStatuses.Selected, SalesUserId, osan, [BusinessUnitCodes.Osan], false, "test_selected"));
        var requestProvider = new DatabaseConnectionStringProvider(
            databases.Configuration, new HttpContextAccessor { HttpContext = requestContext });
        var options = new NotificationOptions
        {
            Dispatch = new NotificationDispatchOptions { RetryCount = 1, ClaimLeaseSeconds = 300, MaxBatchSize = 10 },
            WebPush = new NotificationWebPushOptions { Enabled = true }
        };
        var deliveryStore = new NotificationDeliveryStore(requestProvider, TimeProvider.System, databases.Configuration);

        async Task Write(Guid operationId)
        {
            await using var connection = await databases.OpenAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Runtime, ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await OsanNotificationWriter.WriteAsync(connection, transaction, projectId, operationId,
                OsanNotificationKind.ProjectCreated, SalesUserId, DateTimeOffset.UtcNow, ct,
                recipientIds: [SalesUserId]);
            await transaction.CommitAsync(ct);
        }

        var suppressedOperation = Guid.NewGuid();
        await Write(suppressedOperation);
        Assert.Equal(1, await deliveryStore.CreateImmediateDeliveriesAsync(options, ct, osan));
        Assert.Equal(2L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration, $"""
                select count(*) from notification_deliveries delivery
                join notifications notification on notification.id=delivery.notification_id
                where notification.idempotency_key='{OsanNotificationWriter.IdempotencyKey(projectId, suppressedOperation, OsanNotificationKind.ProjectCreated)}'
                  and delivery.status='Suppressed' and delivery.error_code='SuppressedByUserPreference';
                """, ct));
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration, $"""
                select count(*) from notification_recipients recipient
                join notifications notification on notification.id=recipient.notification_id
                where notification.idempotency_key='{OsanNotificationWriter.IdempotencyKey(projectId, suppressedOperation, OsanNotificationKind.ProjectCreated)}';
                """, ct));

        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            $"delete from osan_notification_preferences where user_id='{SalesUserId}';", ct);
        Assert.Equal(0, await deliveryStore.CreateImmediateDeliveriesAsync(options, ct, osan));

        var queuedOperation = Guid.NewGuid();
        await Write(queuedOperation);
        Assert.Equal(1, await deliveryStore.CreateImmediateDeliveriesAsync(options, ct, osan));
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into osan_notification_preferences(user_id,event_kind,channel,stage_sequence,is_enabled)
            values('{SalesUserId}','ProjectCreated','Mail',0,false),
                  ('{SalesUserId}','ProjectCreated','WebPush',0,false);
            """, ct);

        var mail = new PreferenceCountingHandler(NotificationDeliveryChannels.Mail);
        var push = new PreferenceCountingHandler(NotificationDeliveryChannels.WebPush);
        var dispatcher = new NotificationDispatcher(
            deliveryStore, [mail, push], new StaticOptionsMonitor<NotificationOptions>(options),
            new NotificationWorkerIdentity("osan-preference-test"), requestProvider,
            new BusinessUnitDatabaseBoundaryValidator(
                requestProvider, new MigrationLedgerInspector(catalog), new BusinessUnitDirectoryMigrationCatalog(catalog)),
            NullLogger<NotificationDispatcher>.Instance);
        var queuedKey = OsanNotificationWriter.IdempotencyKey(projectId, queuedOperation, OsanNotificationKind.ProjectCreated);
        var deliveryIds = await databases.ReadColumnAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            select delivery.id::text from notification_deliveries delivery
            join notifications notification on notification.id=delivery.notification_id
            where notification.idempotency_key='{queuedKey}' order by delivery.channel;
            """, ct);
        Assert.Equal(2, deliveryIds.Count);
        foreach (var deliveryId in deliveryIds)
        {
            var result = await dispatcher.DispatchDeliveryAsync(Guid.Parse(deliveryId), null, 1, ct);
            Assert.Equal(NotificationDeliveryStatuses.Suppressed, result.Status);
            Assert.Equal("SuppressedByUserPreference", result.ErrorCode);
        }
        Assert.Equal(0, mail.CallCount);
        Assert.Equal(0, push.CallCount);
        Assert.Equal(2L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration, $"""
                select count(*) from notification_deliveries delivery
                join notifications notification on notification.id=delivery.notification_id
                where notification.idempotency_key='{queuedKey}' and delivery.status='Suppressed';
                """, ct));
    }

    private sealed class PreferenceCountingHandler(string channel) : INotificationChannelHandler
    {
        private int calls;
        public string Channel { get; } = channel;
        public int CallCount => Volatile.Read(ref calls);
        public bool WillCallExternalProvider(NotificationDeliveryMessage message) => true;
        public Task<NotificationChannelResult> SendAsync(NotificationDeliveryMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(NotificationChannelResult.Sent("unexpected-provider-call"));
        }
    }
}
