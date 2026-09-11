using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task OsanWebPush_OwnDeviceLifecycleAndBackgroundDispatchStayInSelectedDatabase()
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
        databases.ConfigurationValues["Notifications:WebPush:Enabled"] = "true";
        // Exercise real provider-result handling, but replace the protocol client below. No network calls.
        databases.ConfigurationValues["Notifications:WebPush:DryRun"] = "false";
        databases.ConfigurationValues["Notifications:WebPush:PublicKey"] = "BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        databases.ConfigurationValues["Notifications:WebPush:PrivateKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAE";
        databases.ConfigurationValues["Notifications:WebPush:Subject"] = "mailto:test@example.invalid";
        databases.ConfigurationValues["Notifications:WebPush:AllowedEndpointHostSuffixes:0"] = "example.test";
        await databases.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            insert into directory_identities(user_id,auth_provider,external_subject,display_name,is_active)
            values('{SalesUserId}','Dev','dev-sales','Sales',true),
                  ('{AdminUserId}','Dev','dev-admin','Administrator',true),
                  ('50000000-0000-0000-0000-000000000007','Dev','dev-viewer','Viewer',true)
            on conflict(user_id) do nothing;
            insert into directory_business_unit_memberships(user_id,business_unit_code,is_active)
            values('{SalesUserId}','OSAN',true),('{AdminUserId}','CHEONGJU',true),
                  ('50000000-0000-0000-0000-000000000007','OSAN',true)
            on conflict(user_id,business_unit_code) do update set is_active=true;
            """, ct);
        var protocol = new IsolationWebPushProtocolClient();
        using var factory = QmsWebApplicationFactory.Create(DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues, includeDefaultDevelopmentAuthentication: true,
            configureTestServices: services =>
            {
                services.RemoveAll<IWebPushProtocolClient>();
                services.AddSingleton<IWebPushProtocolClient>(protocol);
            });
        using var client = factory.CreateClient();
        const string endpoint = "https://push.example.test/shared-browser";
        var subscription = new WebPushSubscriptionRequest(endpoint, new WebPushSubscriptionKeysRequest(
            databases.ConfigurationValues["Notifications:WebPush:PublicKey"]!, "AAAAAAAAAAAAAAAAAAAAAA"));
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null,
            string campus = BusinessUnitCodes.Osan, string? user = null)
        {
            using var request = Request(method, path, user ?? (campus == BusinessUnitCodes.Cheongju ? "dev-admin" : "dev-sales"), campus);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request, ct);
        }
        async Task<bool> Active(string campus)
        {
            using var response = await Send(HttpMethod.Post, "/api/my/web-push/current-status", new { endpoint }, campus);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<WebPushCurrentSubscriptionResponse>(ct))!.Active;
        }
        foreach (var campus in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            using var response = await Send(HttpMethod.Put, "/api/my/web-push/subscriptions", subscription, campus);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{campus}: {response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
            Assert.True(await Active(campus));
        }
        using (var config = await Send(HttpMethod.Get, "/api/my/web-push"))
        {
            Assert.Equal(HttpStatusCode.OK, config.StatusCode);
            Assert.Equal(1, (await config.Content.ReadFromJsonAsync<WebPushConfigurationResponse>(ct))!.ActiveDeviceCount);
        }
        using (var otherUser = await Send(HttpMethod.Post, "/api/my/web-push/current-status", new { endpoint }, user: "dev-viewer"))
            Assert.False((await otherUser.Content.ReadFromJsonAsync<WebPushCurrentSubscriptionResponse>(ct))!.Active);
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Post, "/api/my/web-push"),
            (HttpMethod.Get, "/api/my/web-push/subscriptions"),
            (HttpMethod.Put, "/api/my/web-push/subscriptions/other-user"),
            (HttpMethod.Get, "/api/notification-deliveries")
        })
        {
            using var denied = await Send(method, path, subscription);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        await databases.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            update directory_business_unit_memberships set is_active=false
            where user_id='{SalesUserId}' and business_unit_code='OSAN';
            """, ct);
        using (var denied = await Send(HttpMethod.Put, "/api/my/web-push/subscriptions", subscription))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await databases.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            update directory_business_unit_memberships set is_active=true
            where user_id='{SalesUserId}' and business_unit_code='OSAN';
            """, ct);

        var sharedNotificationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into projects(id,project_key,project_number,name,customer_name,item,project_code,project_title,
                project_title_normalized,delivery_date,sales_owner_user_id,status,created_by_user_id,
                project_profile,osan_product_name,osan_quantity)
            values('{projectId}','push-project','PUSH-PROJECT','Push Project','Synthetic Customer','UL891',
                'PUSH-PROJECT','Push Project','PUSH PROJECT',current_date+30,'{SalesUserId}','Active',
                '{SalesUserId}','Osan','Synthetic Product',1);
            """, ct);
        foreach (var campus in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            var source = campus == BusinessUnitCodes.Osan ? "OsanWorkflow" : "Automatic";
            var recipient = campus == BusinessUnitCodes.Osan ? SalesUserId : AdminUserId;
            var project = campus == BusinessUnitCodes.Osan ? $"'{projectId}'::uuid" : "null";
            await databases.ExecuteAsync(campus, BusinessUnitConnectionPurpose.Migration, $"""
                insert into notifications(id,project_id,notification_type,severity,title,message,idempotency_key,visibility_scope,source_kind)
                values('{sharedNotificationId}',{project},'Info','Info','{campus} PUSH','PRIVATE CONTENT','{sharedNotificationId}','RecipientOnly','{source}');
                insert into notification_recipients(notification_id,user_id) values('{sharedNotificationId}','{recipient}');
                """, ct);
        }
        // None of these records may broaden Osan push recipients or invoke Cheongju planners.
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into notifications(notification_type,severity,title,message,idempotency_key,visibility_scope,source_kind,created_at_utc)
            values('Info','Info','hidden','private','push-hidden','RecipientOnly','OsanWorkflow',now()),
                  ('Info','Info','broadcast','private','push-broadcast','Authenticated','OsanWorkflow',now()),
                  ('Blocking','Critical','Cheongju planner trap','private','push-wrong-source','Authenticated','Automatic',now()),
                  ('Info','Info','old','private','push-before-activation','RecipientOnly','OsanWorkflow','2000-01-01');
            update notifications set project_id='{projectId}' where idempotency_key like 'push-%';
            insert into notification_recipients(notification_id,user_id)
            select id,case when idempotency_key='push-hidden' then '{AdminUserId}'::uuid else '{SalesUserId}'::uuid end
            from notifications where idempotency_key in ('push-hidden','push-before-activation');
            """, ct);
        var dispatcher = factory.Services.GetRequiredService<NotificationDispatcher>();
        // Outside HTTP context: this catches worker fallback/missing-target reads and writes.
        var summary = await dispatcher.DispatchAsync(ct);
        Assert.Equal(2, summary.CreatedDeliveryCount);
        Assert.Equal(2, protocol.Requests.Count);
        foreach (var campus in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            Assert.Equal(1L, await databases.ReadScalarAsync<long>(campus, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from notification_deliveries where channel='WebPush' and status='Sent'", ct));
            Assert.Equal(1L, await databases.ReadScalarAsync<long>(campus, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from web_push_subscription_events where event_type='ProviderAccepted'", ct));
        }
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select count(*) from notification_deliveries", ct));
        Assert.Equal(0, (await dispatcher.DispatchAsync(ct)).CreatedDeliveryCount);
        foreach (var (recipient, project, recipientRow, generation, channel, deliveryType) in new[]
        {
            ($"'{AdminUserId}'::uuid", "project_id", "notification_recipient_id", "web_push_subscription_generation", "WebPush", "WebPushNotification"),
            ("recipient_user_id", "null::uuid", "notification_recipient_id", "web_push_subscription_generation", "WebPush", "WebPushNotification"),
            ("recipient_user_id", "project_id", "null::uuid", "web_push_subscription_generation", "WebPush", "WebPushNotification"),
            ("recipient_user_id", "project_id", "notification_recipient_id", "web_push_subscription_generation+1", "WebPush", "WebPushNotification"),
            ("recipient_user_id", "project_id", "notification_recipient_id", "web_push_subscription_generation", "TeamsActivity", "WebPushNotification"),
            ("recipient_user_id", "project_id", "notification_recipient_id", "web_push_subscription_generation", "WebPush", "OsanWorkflow")
        })
        {
            var denied = await Assert.ThrowsAsync<PostgresException>(() => databases.ExecuteAsync(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Runtime, $"""
                insert into notification_deliveries(notification_id,notification_recipient_id,recipient_user_id,project_id,
                    web_push_subscription_id,web_push_subscription_generation,channel,delivery_type,dedupe_key)
                select notification_id,{recipientRow},{recipient},{project},web_push_subscription_id,{generation},
                    '{channel}','{deliveryType}','forged-{Guid.NewGuid()}'
                from notification_deliveries where notification_id='{sharedNotificationId}';
                """, ct));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
        foreach (var request in protocol.Requests)
        {
            using var payload = JsonDocument.Parse(request.Payload);
            Assert.DoesNotContain("PRIVATE CONTENT", payload.RootElement.GetProperty("body").GetString());
            var campus = payload.RootElement.GetProperty("title").GetString()!.Split(' ')[0];
            Assert.Contains($"businessUnit={campus}", payload.RootElement.GetProperty("url").GetString());
        }
        using (var deactivate = await Send(HttpMethod.Post, "/api/my/web-push/subscriptions/deactivate-current", new { endpoint, reason = "Logout" }))
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.False(await Active(BusinessUnitCodes.Osan));
        Assert.True(await Active(BusinessUnitCodes.Cheongju));
        using (var reactivate = await Send(HttpMethod.Put, "/api/my/web-push/subscriptions", subscription))
            Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        using (var reset = await Send(HttpMethod.Post, "/api/my/web-push/subscriptions/deactivate-all"))
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.False(await Active(BusinessUnitCodes.Osan));
        Assert.True(await Active(BusinessUnitCodes.Cheongju));
        using (var reactivate = await Send(HttpMethod.Put, "/api/my/web-push/subscriptions", subscription))
            Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        Assert.Equal(0, (await dispatcher.DispatchAsync(ct)).CreatedDeliveryCount);
        Assert.Equal(2, protocol.Requests.Count); // Reactivation never replays previous notifications.

        protocol.Exception = new WebPushProtocolException(410, "expired-synthetic-subscription");
        var expiredNotificationId = Guid.NewGuid();
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into notifications(id,project_id,notification_type,severity,title,message,idempotency_key,visibility_scope,source_kind)
            values('{expiredNotificationId}','{projectId}','Info','Info','expired endpoint','private','{expiredNotificationId}','RecipientOnly','OsanWorkflow');
            insert into notification_recipients(notification_id,user_id) values('{expiredNotificationId}','{SalesUserId}');
            """, ct);
        await dispatcher.DispatchAsync(ct);
        Assert.False(await Active(BusinessUnitCodes.Osan));
        Assert.True(await Active(BusinessUnitCodes.Cheongju));
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select count(*) from web_push_subscription_events where event_type='ProviderDeactivated' and reason='WebPushHttp410'", ct));
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Migration,
            "select count(*) from web_push_subscription_events where event_type='ProviderDeactivated'", ct));
    }

    private sealed class IsolationWebPushProtocolClient : IWebPushProtocolClient
    {
        public List<WebPushProtocolRequest> Requests { get; } = [];
        public Exception? Exception { get; set; }
        public Task SendAsync(WebPushProtocolRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }
}
