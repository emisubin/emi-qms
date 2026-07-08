using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class NotificationDeliveryTests
{
    private static readonly Guid DevAdminUserId = new("50000000-0000-0000-0000-000000000001");
    private static readonly Guid DevSalesUserId = new("50000000-0000-0000-0000-000000000002");
    private static readonly Guid DevProductionUserId = new("50000000-0000-0000-0000-000000000003");
    private static readonly Guid DevProcurementUserId = new("50000000-0000-0000-0000-000000000011");
    private static readonly Guid DemoProjectId = new("40000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Dispatcher_CreatesDryRunTeamsAndMailDeliveries_ForUrgentNotification()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Teams:Enabled"] = "true",
            ["Notifications:Teams:DryRun"] = "true",
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true"
        });
        await context.ExecuteSqlAsync("""
            update qms_users
            set email = 'admin@example.test'
            where id = '50000000-0000-0000-0000-000000000001';
            """);
        await context.InsertNotificationAsync(
            "urgent-dryrun",
            "Blocking",
            "Critical",
            "긴급 알림",
            "검수용 긴급 알림입니다.",
            DevAdminUserId);

        var summary = await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, summary.CreatedDeliveryCount);
        Assert.Equal(2, summary.ProcessedDeliveryCount);
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'TeamsChannel' and status = 'DryRunSent';"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'Mail' and status = 'DryRunSent';"));
    }

    [Fact]
    public async Task Dispatcher_SuppressesMailDelivery_WhenRecipientEmailIsMissing()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Teams:Enabled"] = "false",
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true"
        });
        await context.InsertNotificationAsync(
            "urgent-missing-email",
            "Blocking",
            "Critical",
            "긴급 알림",
            "이메일 없는 사용자 알림입니다.",
            DevSalesUserId);

        var summary = await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, summary.CreatedDeliveryCount);
        Assert.Equal(2, summary.ProcessedDeliveryCount);
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'Mail' and status = 'Suppressed' and error_code = 'RecipientEmailMissing';"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'TeamsChannel' and status = 'Disabled' and error_code = 'TeamsDisabled';"));
    }

    [Fact]
    public async Task Dispatcher_DoesNotDuplicateDeliveries_ForSameNotification()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Teams:Enabled"] = "true",
            ["Notifications:Teams:DryRun"] = "true",
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true"
        });
        await context.ExecuteSqlAsync("""
            update qms_users
            set email = 'admin@example.test'
            where id = '50000000-0000-0000-0000-000000000001';
            """);
        await context.InsertNotificationAsync(
            "urgent-dedupe",
            "Blocking",
            "Critical",
            "긴급 알림",
            "중복 방지 알림입니다.",
            DevAdminUserId);

        await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);
        await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries;"));
    }

    [Fact]
    public async Task AutomaticUrgentDeliveries_UseCommonExternalFormatAndDisplaySnapshots()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync();
        await context.ExecuteSqlAsync("""
            update qms_users
            set email = 'admin@example.test'
            where id = '50000000-0000-0000-0000-000000000001';
            """);
        await context.InsertNotificationAsync(
            "urgent-common-format",
            "Blocking",
            "Critical",
            "LQC 부적합",
            "조치 담당자 확인이 필요합니다.",
            DevAdminUserId);

        var created = await context.DeliveryStore.CreateImmediateDeliveriesAsync(
            context.NotificationOptions.CurrentValue,
            TestContext.Current.CancellationToken);
        var deliveries = await context.DeliveryStore.GetDueDeliveriesAsync(10, 3, TestContext.Current.CancellationToken);
        var mailDelivery = Assert.Single(deliveries, item => item.Channel == NotificationDeliveryChannels.Mail);
        var teamsChannelDelivery = Assert.Single(deliveries, item => item.Channel == NotificationDeliveryChannels.TeamsChannel);

        var mailMessage = await context.DeliveryStore.RenderMessageAsync(mailDelivery, TestContext.Current.CancellationToken);
        var teamsChannelMessage = await context.DeliveryStore.RenderMessageAsync(teamsChannelDelivery, TestContext.Current.CancellationToken);

        Assert.Equal(2, created);
        Assert.Equal("[긴급 알림] LQC 부적합", mailMessage.Subject);
        Assert.Equal("EMI 프로젝트 통합관리시스템 알림", teamsChannelMessage.Subject);
        Assert.Contains("알림 유형: 긴급 알림", mailMessage.Body, StringComparison.Ordinal);
        Assert.Contains("프로젝트명: Demo Project Alpha", mailMessage.Body, StringComparison.Ordinal);
        Assert.Contains("제목: LQC 부적합", mailMessage.Body, StringComparison.Ordinal);
        Assert.Contains("내용:", mailMessage.Body, StringComparison.Ordinal);
        Assert.Contains("조치 담당자 확인이 필요합니다.", mailMessage.Body, StringComparison.Ordinal);
        Assert.Contains("발송시각: 2026-07-03 09:00", mailMessage.Body, StringComparison.Ordinal);
        Assert.EndsWith("끝.", mailMessage.Body.Trim(), StringComparison.Ordinal);
        Assert.Equal(mailMessage.Body, teamsChannelMessage.Body);
        Assert.DoesNotContain("Correlation", mailMessage.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Correlation", mailMessage.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("유형: Blocking", mailMessage.Body, StringComparison.Ordinal);
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'Mail' and display_title = 'LQC 부적합' and display_recipient_email = 'admin@example.test';"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'TeamsChannel' and display_channel_target = 'Teams 채널' and display_project_name = 'Demo Project Alpha';"));
    }

    [Fact]
    public async Task DailyDigest_CreatesDryRunMail_WhenUserHasOpenWork()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:DailyDigest:Enabled"] = "true",
            ["Notifications:DailyDigest:Time"] = "07:30",
            ["Notifications:DailyDigest:TimeZone"] = "Asia/Seoul",
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true"
        });
        await context.ExecuteSqlAsync("""
            update qms_users
            set email = 'admin@example.test'
            where id = '50000000-0000-0000-0000-000000000001';

            insert into work_items (
                project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                assigned_user_id, assigned_role_code, title, description, status, priority,
                idempotency_key, created_by_user_id
            )
            values (
                '40000000-0000-0000-0000-000000000001',
                'Project',
                '40000000-0000-0000-0000-000000000001',
                'ProductionPlanning',
                'ProductionPlanningPrimary',
                '50000000-0000-0000-0000-000000000001',
                'system-administrator',
                '일일 요약 테스트 업무',
                '일일 요약 테스트',
                'Requested',
                'Normal',
                'daily-digest-open-work',
                '50000000-0000-0000-0000-000000000001'
            );
            """);

        var summary = await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.CreatedDigestDeliveryCount);
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where delivery_type = 'DailyDigest' and status = 'DryRunSent';"));
    }

    [Fact]
    public async Task DailyDigest_DoesNotCreateDelivery_WhenUserHasNoContent()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:DailyDigest:Enabled"] = "true",
            ["Notifications:DailyDigest:Time"] = "07:30",
            ["Notifications:DailyDigest:TimeZone"] = "Asia/Seoul",
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true"
        });
        await context.ExecuteSqlAsync("""
            delete from notification_deliveries;
            delete from notification_recipients;
            delete from notifications;
            delete from work_items;
            delete from project_assignees;
            """);

        var summary = await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, summary.CreatedDigestDeliveryCount);
        Assert.Equal(0L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where delivery_type = 'DailyDigest';"));
    }

    [Fact]
    public async Task DailyDigest_IncludesAssignedProjectSummary_AndGroupsRoles()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:DailyDigest:Enabled"] = "true",
            ["Notifications:DailyDigest:Time"] = "07:30",
            ["Notifications:DailyDigest:TimeZone"] = "Asia/Seoul",
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true"
        });
        await context.ExecuteSqlAsync("""
            delete from notification_deliveries;
            delete from notification_recipients;
            delete from notifications;
            delete from work_items;
            delete from project_assignees;

            update qms_users
            set email = 'admin@example.test'
            where id = '50000000-0000-0000-0000-000000000001';

            update projects
            set project_title = 'UL67 SAMPLE PROJECT',
                project_title_normalized = 'UL67 SAMPLE PROJECT',
                delivery_date = '2026-07-31',
                status = 'Active',
                deleted_at_utc = null
            where id = '40000000-0000-0000-0000-000000000001';

            update projects
            set project_title = 'DELETED DIGEST PROJECT',
                project_title_normalized = 'DELETED DIGEST PROJECT',
                delivery_date = '2026-08-10',
                status = 'Active',
                deleted_at_utc = '2026-07-03T00:00:00Z'
            where id = '40000000-0000-0000-0000-000000000002';

            insert into project_assignees (
                project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc
            )
            values
                ('40000000-0000-0000-0000-000000000001', 'SalesPrimary', '50000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000001', '2026-07-03T00:00:00Z'),
                ('40000000-0000-0000-0000-000000000001', 'QualityIQC', '50000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000001', '2026-07-03T00:00:00Z'),
                ('40000000-0000-0000-0000-000000000002', 'ProductionPlanningPrimary', '50000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000001', '2026-07-03T00:00:00Z');
            """);

        var created = await context.DeliveryStore.CreateDailyDigestDeliveriesIfDueAsync(
            context.NotificationOptions.CurrentValue,
            TestContext.Current.CancellationToken);
        var deliveries = await context.DeliveryStore.GetDueDeliveriesAsync(10, 3, TestContext.Current.CancellationToken);
        var delivery = Assert.Single(deliveries, item => item.DeliveryType == NotificationDeliveryTypes.DailyDigest);

        var message = await context.DeliveryStore.RenderMessageAsync(delivery, TestContext.Current.CancellationToken);

        Assert.Equal(1, created);
        Assert.StartsWith("[일일 업무 요약] ", message.Subject, StringComparison.Ordinal);
        Assert.Contains("EMI 프로젝트 통합관리시스템 알림", message.Body, StringComparison.Ordinal);
        Assert.Contains("알림 유형: 일일 업무 요약", message.Body, StringComparison.Ordinal);
        Assert.Contains("프로젝트명: 여러 프로젝트", message.Body, StringComparison.Ordinal);
        Assert.Contains("제목:", message.Body, StringComparison.Ordinal);
        Assert.Contains("발송시각: 2026-07-03 09:00", message.Body, StringComparison.Ordinal);
        Assert.EndsWith("끝.", message.Body.Trim(), StringComparison.Ordinal);
        Assert.Contains("내 담당 프로젝트 요약", message.Body, StringComparison.Ordinal);
        Assert.Contains("UL67 SAMPLE PROJECT", message.Body, StringComparison.Ordinal);
        Assert.Contains("납기일 2026-07-31", message.Body, StringComparison.Ordinal);
        Assert.Contains("영업 정담당자", message.Body, StringComparison.Ordinal);
        Assert.Contains("IQC 정담당자", message.Body, StringComparison.Ordinal);
        Assert.Contains($"/projects/{DemoProjectId}", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("SalesPrimary", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETED DIGEST PROJECT", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DailyDigest_OmitsAssignedProjectSummary_WhenUserHasNoAssignedProjects()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:DailyDigest:Enabled"] = "true",
            ["Notifications:DailyDigest:Time"] = "07:30",
            ["Notifications:DailyDigest:TimeZone"] = "Asia/Seoul",
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true"
        });
        await context.ExecuteSqlAsync("""
            delete from notification_deliveries;
            delete from notification_recipients;
            delete from notifications;
            delete from work_items;
            delete from project_assignees;

            update qms_users
            set email = 'admin@example.test'
            where id = '50000000-0000-0000-0000-000000000001';

            insert into work_items (
                project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                assigned_user_id, assigned_role_code, title, description, status, priority,
                idempotency_key, created_by_user_id
            )
            values (
                '40000000-0000-0000-0000-000000000001',
                'Project',
                '40000000-0000-0000-0000-000000000001',
                'ProductionPlanning',
                'ProductionPlanningPrimary',
                '50000000-0000-0000-0000-000000000001',
                'system-administrator',
                '담당 프로젝트 없는 요약 테스트 업무',
                '담당 프로젝트 없는 요약 테스트입니다.',
                'Requested',
                'Normal',
                'daily-digest-no-assigned-project',
                '50000000-0000-0000-0000-000000000001'
            );
            """);

        var created = await context.DeliveryStore.CreateDailyDigestDeliveriesIfDueAsync(
            context.NotificationOptions.CurrentValue,
            TestContext.Current.CancellationToken);
        var deliveries = await context.DeliveryStore.GetDueDeliveriesAsync(10, 3, TestContext.Current.CancellationToken);
        var delivery = Assert.Single(deliveries, item => item.DeliveryType == NotificationDeliveryTypes.DailyDigest);

        var message = await context.DeliveryStore.RenderMessageAsync(delivery, TestContext.Current.CancellationToken);

        Assert.Equal(1, created);
        Assert.Contains("내 미완료 업무", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("내 담당 프로젝트 요약", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatcher_RetriesFailedTeamsWebhookDelivery_UpToConfiguredLimit()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(
            new Dictionary<string, string?>
            {
                ["Notifications:Teams:Enabled"] = "true",
                ["Notifications:Teams:DryRun"] = "false",
                ["Notifications:Teams:WebhookUrl"] = "https://example.test/webhook",
                ["Notifications:Mail:Enabled"] = "false",
                ["Notifications:Dispatch:RetryCount"] = "3"
            },
            services =>
            {
                var descriptors = services.Where(service => service.ServiceType == typeof(ITeamsWebhookClient)).ToList();
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<ITeamsWebhookClient, FailingTeamsWebhookClient>();
            });
        await context.InsertNotificationAsync(
            "urgent-retry",
            "Blocking",
            "Critical",
            "긴급 알림",
            "retry 테스트 알림입니다.",
            DevAdminUserId);

        await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);
        await context.ResetNextAttemptAsync(NotificationDeliveryChannels.TeamsChannel);
        await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);
        await context.ResetNextAttemptAsync(NotificationDeliveryChannels.TeamsChannel);
        await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, await context.ReadScalarAsync<int>("select attempt_count from notification_deliveries where channel = 'TeamsChannel';"));
        Assert.Equal("Failed", await context.ReadScalarAsync<string>("select status from notification_deliveries where channel = 'TeamsChannel';"));
        Assert.Equal(0L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'TeamsChannel' and next_attempt_at_utc is not null;"));
    }

    [Fact]
    public void TeamsWebhookPayload_UsesAdaptiveCardRoot()
    {
        var payload = TeamsWebhookPayload.FromMessage(new NotificationDeliveryMessage(
            Guid.NewGuid(),
            NotificationDeliveryChannels.TeamsChannel,
            NotificationDeliveryTypes.UrgentBlocking,
            "TASK-NOTIFY-001 Teams Webhook 테스트",
            "EMI 프로젝트 통합관리시스템 UAT 테스트 알림입니다. 실제 업무 알림이 아닙니다.",
            "/notifications",
            "통합 채널",
            null));

        var json = JsonSerializer.Serialize(payload);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("http://adaptivecards.io/schemas/adaptive-card.json", root.GetProperty("$schema").GetString());
        Assert.Equal("AdaptiveCard", root.GetProperty("type").GetString());
        Assert.Equal("1.4", root.GetProperty("version").GetString());
        var body = root.GetProperty("body");
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal("TextBlock", body[0].GetProperty("type").GetString());
        Assert.Equal("TASK-NOTIFY-001 Teams Webhook 테스트", body[0].GetProperty("text").GetString());
        Assert.Equal("TextBlock", body[1].GetProperty("type").GetString());
        Assert.Contains("실제 업무 알림이 아닙니다", body[1].GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.Equal(2, body.GetArrayLength());
    }

    [Fact]
    public async Task TeamsWebhookClient_PostsAdaptiveCardAsApplicationJson()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new TeamsWebhookClient(new HttpClient(handler));
        var payload = TeamsWebhookPayload.FromMessage(new NotificationDeliveryMessage(
            Guid.NewGuid(),
            NotificationDeliveryChannels.TeamsChannel,
            NotificationDeliveryTypes.UrgentBlocking,
            "테스트 제목",
            "테스트 본문",
            null,
            null,
            null));

        var providerMessageId = await client.PostAsync("https://example.test/webhook", payload, TestContext.Current.CancellationToken);

        Assert.Equal("test-request-id", providerMessageId);
        Assert.StartsWith("application/json", handler.ContentType, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal("AdaptiveCard", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void TeamsActivityRenderer_MapsDeadlineOverduePayload()
    {
        var result = TeamsActivityNotificationRenderer.Render(
            new NotificationDeliveryMessage(
                Guid.NewGuid(),
                NotificationDeliveryChannels.TeamsActivity,
                NotificationDeliveryTypes.OverdueL2,
                "지연 업무",
                "예정일이 지난 업무입니다.",
                "/projects/40000000-0000-0000-0000-000000000001",
                "담당자",
                null),
            new NotificationTeamsActivityOptions
            {
                TopicWebUrl = "https://teams.microsoft.com/l/entity/app/home"
            });

        Assert.Equal("deadlineOverdue", result.ActivityType);
        Assert.Equal("text", result.TopicSource);
        Assert.Equal("text", GraphTeamsActivityNotificationRequest.FromRequest(new TeamsActivitySendRequest(
            "user-id",
            result.ActivityType,
            result.TopicSource,
            result.TopicValue,
            result.TopicWebUrl,
            result.PreviewText,
            result.TemplateParameters,
            "teams-app-id",
            "ABC123")).Topic.Source);
        Assert.Equal("https://teams.microsoft.com/l/entity/app/home", result.TopicWebUrl);
        Assert.Equal("예정일이 지난 업무입니다.", result.PreviewText);
        Assert.True(result.PreviewText.Length <= 150);
        Assert.Equal("L2", result.TemplateParameters["escalationLevel"]);
        Assert.Equal("지연 업무", result.TemplateParameters["taskName"]);
    }

    [Fact]
    public void TeamsActivityRenderer_UsesInstalledAppEntityUrlTopic_WhenProvided()
    {
        const string entityUrl = "https://graph.microsoft.com/v1.0/users/user-id/teamwork/installedApps/installation-id";
        var result = TeamsActivityNotificationRenderer.Render(
            new NotificationDeliveryMessage(
                Guid.NewGuid(),
                NotificationDeliveryChannels.TeamsActivity,
                NotificationDeliveryTypes.ManualTest,
                "테스트 업무",
                "테스트 본문",
                "/projects/40000000-0000-0000-0000-000000000001",
                "담당자",
                null,
                TeamsActivityType: "workItemAssigned",
                TeamsActivityTopicSource: "entityUrl",
                TeamsActivityTopicValue: entityUrl),
            new NotificationTeamsActivityOptions
            {
                TopicWebUrl = "https://teams.microsoft.com/l/entity/app/home"
            });

        var graphRequest = GraphTeamsActivityNotificationRequest.FromRequest(new TeamsActivitySendRequest(
            "user-id",
            result.ActivityType,
            result.TopicSource,
            result.TopicValue,
            result.TopicWebUrl,
            result.PreviewText,
            result.TemplateParameters,
            "teams-app-id",
            "ABC123"));

        Assert.Equal("entityUrl", result.TopicSource);
        Assert.Equal(entityUrl, graphRequest.Topic.Value);
        Assert.Equal("entityUrl", graphRequest.Topic.Source);
        Assert.Equal("workItemAssigned", result.ActivityType);
        Assert.Equal("테스트 업무", result.TemplateParameters["taskName"]);
        Assert.Equal("테스트 본문", result.PreviewText);
        Assert.True(result.PreviewText.Length <= 150);
        Assert.Null(graphRequest.Topic.WebUrl);
        Assert.Null(graphRequest.TeamsAppId);
    }

    [Fact]
    public void TeamsActivityRenderer_ProjectCreationManualTestPreview_IsShort()
    {
        var result = TeamsActivityNotificationRenderer.Render(
            new NotificationDeliveryMessage(
                Guid.NewGuid(),
                NotificationDeliveryChannels.TeamsActivity,
                NotificationDeliveryTypes.ManualTest,
                "[테스트] 프로젝트 생성 알림",
                "EMI 프로젝트 통합관리시스템 프로젝트 생성 알림 3채널 최종 검수입니다. 실제 업무 알림이 아닙니다. 긴 본문은 Activity Feed 목록에 그대로 표시하지 않습니다.",
                "/teams/activity",
                "담당자",
                null,
                TeamsActivityType: "workItemAssigned"),
            new NotificationTeamsActivityOptions
            {
                TopicWebUrl = "https://teams.microsoft.com/l/entity/app/home"
            });

        Assert.StartsWith("EMI 프로젝트 통합관리시스템 프로젝트 생성 알림", result.PreviewText, StringComparison.Ordinal);
        Assert.True(result.PreviewText.Length <= 150);
        Assert.Equal("workItemAssigned", result.ActivityType);
        Assert.Equal("[테스트] 프로젝트 생성 알림", result.TemplateParameters["taskName"]);
    }

    [Fact]
    public async Task TeamsActivityChannelHandler_DryRun_DoesNotCallGraphClient()
    {
        var client = new CapturingTeamsActivityClient();
        var handler = new TeamsActivityChannelHandler(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                TeamsActivity = new NotificationTeamsActivityOptions
                {
                    Enabled = true,
                    DryRun = true,
                    TopicWebUrl = "https://qms.example.test"
                }
            }),
            client);

        var result = await handler.SendAsync(CreateTeamsActivityDeliveryMessage(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.DryRunSent, result.Status);
        Assert.False(client.WasCalled);
    }

    [Fact]
    public async Task TeamsActivityChannelHandler_ActualEntraUser_CallsGraphClient()
    {
        var client = new CapturingTeamsActivityClient();
        var handler = new TeamsActivityChannelHandler(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                TeamsActivity = new NotificationTeamsActivityOptions
                {
                    Enabled = true,
                    DryRun = false,
                    TopicWebUrl = "https://teams.microsoft.com/l/entity/app/home",
                    TeamsAppId = "teams-app-id"
                }
            }),
            client);

        var result = await handler.SendAsync(CreateTeamsActivityDeliveryMessage(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Sent, result.Status);
        Assert.True(client.WasCalled);
        Assert.NotNull(client.Request);
        Assert.Equal("entra-user-object-id", client.Request.UserId);
        Assert.Equal("workItemAssigned", client.Request.ActivityType);
        Assert.Equal("teams-app-id", client.Request.TeamsAppId);
    }

    [Fact]
    public async Task TeamsActivityChannelHandler_ActualDevUser_IsSuppressed()
    {
        var client = new CapturingTeamsActivityClient();
        var handler = new TeamsActivityChannelHandler(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                TeamsActivity = new NotificationTeamsActivityOptions
                {
                    Enabled = true,
                    DryRun = false,
                    TopicWebUrl = "https://qms.example.test"
                }
            }),
            client);

        var result = await handler.SendAsync(
            CreateTeamsActivityDeliveryMessage(
                authProvider: "Dev",
                entraObjectId: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Suppressed, result.Status);
        Assert.Equal("TeamsActivityUserNotEntra", result.ErrorCode);
        Assert.False(client.WasCalled);
    }

    [Fact]
    public async Task GraphTeamsActivityClient_NoContent_ReturnsSentAndBuildsGraphRequest()
    {
        var handler = new TeamsActivityHttpMessageHandler(HttpStatusCode.NoContent);
        var client = new GraphTeamsActivityClient(
            new HttpClient(handler),
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                TeamsActivity = new NotificationTeamsActivityOptions
                {
                    TenantId = "tenant-id",
                    ClientId = "teams-activity-client-id",
                    ClientSecret = "placeholder-secret",
                    TopicWebUrl = "https://teams.microsoft.com/l/entity/app/home"
                }
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero)));

        var result = await client.SendAsync(
            new TeamsActivitySendRequest(
                "entra-user-object-id",
                "workItemAssigned",
                "text",
                "테스트 업무",
                "https://teams.microsoft.com/l/entity/app/home",
                "테스트 업무가 배정되었습니다.",
                new Dictionary<string, string>
                {
                    ["taskName"] = "테스트 업무"
                },
                "teams-app-id",
                "ABC123"),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Sent, result.Status);
        Assert.Contains("teams-activity-sent", result.ProviderMessageId, StringComparison.Ordinal);
        Assert.Equal("https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token", handler.TokenRequestUri?.ToString());
        Assert.Contains("grant_type=client_credentials", handler.TokenRequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", handler.TokenRequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/users/entra-user-object-id/teamwork/sendActivityNotification", handler.ActivityRequestUri?.ToString(), StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("ABC123", handler.ClientRequestId);
        using var document = JsonDocument.Parse(Assert.IsType<string>(handler.ActivityRequestBody));
        var root = document.RootElement;
        Assert.Equal("workItemAssigned", root.GetProperty("activityType").GetString());
        Assert.Equal("teams-app-id", root.GetProperty("teamsAppId").GetString());
        Assert.Equal("text", root.GetProperty("topic").GetProperty("source").GetString());
        Assert.Equal("https://teams.microsoft.com/l/entity/app/home", root.GetProperty("topic").GetProperty("webUrl").GetString());
        Assert.Equal("테스트 업무가 배정되었습니다.", root.GetProperty("previewText").GetProperty("content").GetString());
        Assert.Equal("taskName", root.GetProperty("templateParameters")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GraphTeamsActivityClient_TextTopicWithNonTeamsWebUrl_FailsBeforeGraphRequest()
    {
        var handler = new TeamsActivityHttpMessageHandler(HttpStatusCode.NoContent);
        var client = new GraphTeamsActivityClient(
            new HttpClient(handler),
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                TeamsActivity = new NotificationTeamsActivityOptions
                {
                    TenantId = "tenant-id",
                    ClientId = "teams-activity-client-id",
                    ClientSecret = "placeholder-secret"
                }
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero)));

        var result = await client.SendAsync(
            new TeamsActivitySendRequest(
                "entra-user-object-id",
                "workItemAssigned",
                "text",
                "테스트 업무",
                "https://qms.example.test/projects/1",
                "테스트 업무가 배정되었습니다.",
                new Dictionary<string, string>
                {
                    ["taskName"] = "테스트 업무"
                },
                "teams-app-id",
                "ABC123"),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.Equal("TeamsActivityInvalidTopic", result.ErrorCode);
        Assert.Null(handler.ActivityRequestUri);
    }

    [Fact]
    public async Task GraphTeamsActivityClient_EntityUrlTopic_DoesNotRequireTopicWebUrlOrTeamsAppId()
    {
        var handler = new TeamsActivityHttpMessageHandler(HttpStatusCode.NoContent);
        var client = new GraphTeamsActivityClient(
            new HttpClient(handler),
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                TeamsActivity = new NotificationTeamsActivityOptions
                {
                    TenantId = "tenant-id",
                    ClientId = "teams-activity-client-id",
                    ClientSecret = "placeholder-secret"
                }
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero)));

        var result = await client.SendAsync(
            new TeamsActivitySendRequest(
                "entra-user-object-id",
                "workItemAssigned",
                "entityUrl",
                "https://graph.microsoft.com/v1.0/users/entra-user-object-id/teamwork/installedApps/installation-id",
                null,
                "테스트 업무가 배정되었습니다.",
                new Dictionary<string, string>
                {
                    ["taskName"] = "테스트 업무"
                },
                "teams-app-id",
                "ABC123"),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Sent, result.Status);
        using var document = JsonDocument.Parse(Assert.IsType<string>(handler.ActivityRequestBody));
        var root = document.RootElement;
        Assert.Equal("entityUrl", root.GetProperty("topic").GetProperty("source").GetString());
        Assert.False(root.GetProperty("topic").TryGetProperty("webUrl", out _));
        Assert.False(root.TryGetProperty("teamsAppId", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "TeamsActivityPermissionDenied")]
    [InlineData(HttpStatusCode.NotFound, "TeamsActivityUserOrAppNotFound")]
    [InlineData(HttpStatusCode.BadRequest, "TeamsActivityInvalidRequest")]
    [InlineData(HttpStatusCode.TooManyRequests, "TeamsActivityThrottled")]
    public async Task GraphTeamsActivityClient_MapsGraphErrors(HttpStatusCode statusCode, string expectedErrorCode)
    {
        var client = new GraphTeamsActivityClient(
            new HttpClient(new TeamsActivityHttpMessageHandler(statusCode)),
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                TeamsActivity = new NotificationTeamsActivityOptions
                {
                    TenantId = "tenant-id",
                    ClientId = "teams-activity-client-id",
                    ClientSecret = "placeholder-secret",
                    TopicWebUrl = "https://teams.microsoft.com/l/entity/app/home"
                }
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero)));

        var result = await client.SendAsync(
            new TeamsActivitySendRequest(
                "entra-user-object-id",
                "workItemAssigned",
                "text",
                "테스트 업무",
                "https://teams.microsoft.com/l/entity/app/home",
                "테스트 업무가 배정되었습니다.",
                new Dictionary<string, string>
                {
                    ["taskName"] = "테스트 업무"
                },
                null,
                null),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Contains("teams-activity-sent", result.ProviderMessageId, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Failed to find Teams application '00000000-0000-0000-0000-000000000000' within the recipient's installed applications.", "TeamsActivityAppNotInstalled")]
    [InlineData("Invalid 'webUrl' specified, Weburl must start with a valid Microsoft Teams domain followed by '/l/'.", "TeamsActivityInvalidTopic")]
    public async Task GraphTeamsActivityClient_MapsGraphBadRequestBody(string graphMessage, string expectedErrorCode)
    {
        var handler = new TeamsActivityHttpMessageHandler(
            HttpStatusCode.BadRequest,
            $$"""
            {
              "error": {
                "code": "BadRequest",
                "message": "{{graphMessage}}"
              }
            }
            """);
        var client = new GraphTeamsActivityClient(
            new HttpClient(handler),
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                TeamsActivity = new NotificationTeamsActivityOptions
                {
                    TenantId = "tenant-id",
                    ClientId = "teams-activity-client-id",
                    ClientSecret = "placeholder-secret",
                    TopicWebUrl = "https://teams.microsoft.com/l/entity/app/home"
                }
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero)));

        var result = await client.SendAsync(
            new TeamsActivitySendRequest(
                "entra-user-object-id",
                "workItemAssigned",
                "text",
                "테스트 업무",
                "https://teams.microsoft.com/l/entity/app/home",
                "테스트 업무가 배정되었습니다.",
                new Dictionary<string, string>
                {
                    ["taskName"] = "테스트 업무"
                },
                null,
                "ABC123"),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.DoesNotContain("00000000-0000-0000-0000-000000000000", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraphClientCredentialsTokenProvider_RequestsClientCredentialsToken()
    {
        var handler = new GraphTokenHttpMessageHandler();
        var provider = new GraphClientCredentialsTokenProvider(
            new HttpClient(handler),
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                Graph = new NotificationGraphOptions
                {
                    TenantId = "tenant-id",
                    ClientId = "notifications-client-id",
                    ClientSecret = "placeholder-secret"
                }
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero)));

        var token = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        Assert.True(token.Succeeded);
        Assert.Equal("https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token", handler.RequestUri?.ToString());
        Assert.Contains("grant_type=client_credentials", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("scope=https%3A%2F%2Fgraph.microsoft.com%2F.default", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GraphMailClient_SendMailAccepted_ReturnsSentAndBuildsGraphRequest()
    {
        var handler = new CapturingHttpMessageHandler(HttpStatusCode.Accepted);
        var client = new GraphMailClient(
            new HttpClient(handler),
            new StubGraphTokenProvider("placeholder-token"),
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                Mail = new NotificationMailOptions
                {
                    Provider = "Graph",
                    SenderAddress = "sender@example.test"
                }
            }));

        var result = await client.SendAsync(
            new MailDeliveryPayload(
                "recipient@example.test",
                "TASK-NOTIFY-001 Graph Mail 테스트",
                "테스트 메일 본문입니다.",
                null,
                "sender@example.test",
                SaveToSentItems: true,
                CorrelationId: "ABC123"),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Sent, result.Status);
        Assert.Contains("graph-sendmail-accepted", result.ProviderMessageId, StringComparison.Ordinal);
        Assert.Contains("request-id=test-request-id", result.ProviderMessageId, StringComparison.Ordinal);
        Assert.Contains("client-request-id=ABC123", result.ProviderMessageId, StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("ABC123", handler.ClientRequestId);
        Assert.Equal("true", handler.ReturnClientRequestId);
        Assert.EndsWith("/users/sender%40example.test/sendMail", handler.RequestUri?.ToString(), StringComparison.Ordinal);
        using var document = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var root = document.RootElement;
        Assert.True(root.GetProperty("saveToSentItems").GetBoolean());
        Assert.Equal("TASK-NOTIFY-001 Graph Mail 테스트", root.GetProperty("message").GetProperty("subject").GetString());
        Assert.Equal("recipient@example.test", root.GetProperty("message").GetProperty("toRecipients")[0].GetProperty("emailAddress").GetProperty("address").GetString());
        var headers = root.GetProperty("message").GetProperty("internetMessageHeaders");
        Assert.Equal("x-emi-notification-test-id", headers[0].GetProperty("name").GetString());
        Assert.Equal("ABC123", headers[0].GetProperty("value").GetString());
    }

    [Fact]
    public async Task GraphMailClient_Forbidden_ReturnsPermissionFailure()
    {
        var client = new GraphMailClient(
            new HttpClient(new CapturingHttpMessageHandler(HttpStatusCode.Forbidden)),
            new StubGraphTokenProvider("placeholder-token"),
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                Mail = new NotificationMailOptions
                {
                    Provider = "Graph",
                    SenderAddress = "sender@example.test"
                }
            }));

        var result = await client.SendAsync(
            new MailDeliveryPayload(
                "recipient@example.test",
                "권한 실패 테스트",
                "권한 실패 테스트 본문입니다.",
                null,
                "sender@example.test"),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.Equal("GraphMailForbidden", result.ErrorCode);
    }

    [Fact]
    public async Task ConfiguredMailClient_SelectsSmtpProvider()
    {
        var smtp = new CapturingSmtpMailClient();
        var graph = new CapturingGraphMailClient();
        var client = new ConfiguredMailClient(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                Mail = new NotificationMailOptions
                {
                    Provider = "Smtp"
                }
            }),
            smtp,
            graph);

        var result = await client.SendAsync(CreateMailPayload(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Sent, result.Status);
        Assert.True(smtp.WasCalled);
        Assert.False(graph.WasCalled);
    }

    [Fact]
    public async Task ConfiguredMailClient_SelectsGraphProvider()
    {
        var smtp = new CapturingSmtpMailClient();
        var graph = new CapturingGraphMailClient();
        var client = new ConfiguredMailClient(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                Mail = new NotificationMailOptions
                {
                    Provider = "Graph"
                }
            }),
            smtp,
            graph);

        var result = await client.SendAsync(CreateMailPayload(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Sent, result.Status);
        Assert.False(smtp.WasCalled);
        Assert.True(graph.WasCalled);
    }

    [Fact]
    public async Task MailChannelHandler_DryRunProvider_BypassesActualMailClient()
    {
        var handler = new MailChannelHandler(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                Mail = new NotificationMailOptions
                {
                    Enabled = true,
                    DryRun = false,
                    Provider = "DryRun"
                }
            }),
            new ThrowingMailClient());

        var result = await handler.SendAsync(CreateMailDeliveryMessage(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.DryRunSent, result.Status);
    }

    [Fact]
    public async Task SmtpMailClient_SendsMailThroughTransport()
    {
        var transport = new CapturingSmtpMailTransport();
        var client = new SmtpMailClient(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                Mail = new NotificationMailOptions
                {
                    SenderAddress = "sender@example.test",
                    SenderDisplayName = "EMI 프로젝트 통합관리시스템 알림",
                    Smtp = new NotificationSmtpOptions
                    {
                        Host = "smtp.gmail.com",
                        Port = 587,
                        Security = "StartTls",
                        Username = "sender@example.test",
                        Password = "placeholder-app-password",
                        TimeoutSeconds = 15
                    }
                }
            }),
            transport);

        var result = await client.SendAsync(
            new MailDeliveryPayload(
                "recipient@example.test",
                "TASK-NOTIFY-001 Gmail SMTP 테스트 [ABC123]",
                "Correlation ID: ABC123",
                null,
                "sender@example.test",
                CorrelationId: "ABC123"),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Sent, result.Status);
        Assert.Equal("smtp-sent;client-request-id=ABC123", result.ProviderMessageId);
        Assert.NotNull(transport.Request);
        Assert.Equal("smtp.gmail.com", transport.Request.Host);
        Assert.Equal(587, transport.Request.Port);
        Assert.Equal("StartTls", transport.Request.Security);
        Assert.Equal("EMI 프로젝트 통합관리시스템 알림", transport.Request.SenderDisplayName);
        Assert.Equal("sender@example.test", transport.Request.SenderAddress);
        Assert.Equal("recipient@example.test", transport.Request.RecipientEmail);
        Assert.Equal("ABC123", transport.Request.CorrelationId);
    }

    [Fact]
    public async Task SmtpMailClient_ReturnsConfigError_WhenRequiredSmtpSettingsAreMissing()
    {
        var client = new SmtpMailClient(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                Mail = new NotificationMailOptions
                {
                    SenderAddress = "sender@example.test"
                }
            }),
            new CapturingSmtpMailTransport());

        var result = await client.SendAsync(CreateMailPayload(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.Equal("SmtpConfigMissing", result.ErrorCode);
    }

    [Fact]
    public async Task SmtpMailClient_ReturnsAuthenticationFailure_WhenTransportAuthFails()
    {
        var client = new SmtpMailClient(
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions
            {
                Mail = new NotificationMailOptions
                {
                    SenderAddress = "sender@example.test",
                    Smtp = new NotificationSmtpOptions
                    {
                        Host = "smtp.gmail.com",
                        Port = 587,
                        Security = "StartTls",
                        Username = "sender@example.test",
                        Password = "placeholder-app-password"
                    }
                }
            }),
            new ThrowingSmtpMailTransport("SmtpAuthenticationFailed"));

        var result = await client.SendAsync(CreateMailPayload(), TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.Equal("SmtpAuthenticationFailed", result.ErrorCode);
    }

    [Fact]
    public async Task AdminDeliveryEndpoint_IsSystemAdministratorOnly()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync();
        using var adminClient = context.CreateClient("dev-admin");
        using var salesClient = context.CreateClient("dev-sales");

        var forbidden = await salesClient.GetAsync("/api/admin/notification-deliveries", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var ok = await adminClient.GetAsync("/api/admin/notification-deliveries", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        using var body = await JsonDocument.ParseAsync(await ok.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(body.RootElement.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task AdminDeliveryEndpoint_FiltersByStatus()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync();
        using var adminClient = context.CreateClient("dev-admin");
        await context.ExecuteSqlAsync("""
            insert into notification_deliveries (
                recipient_user_id, project_id, channel, delivery_type, status, attempt_count,
                next_attempt_at_utc, error_code, error_message, dedupe_key, group_key
            )
            values
                (
                    '50000000-0000-0000-0000-000000000002',
                    '40000000-0000-0000-0000-000000000001',
                    'Mail',
                    'DailyDigest',
                    'Failed',
                    1,
                    null,
                    'RecipientEmailMissing',
                    '수신자 이메일이 없습니다.',
                    'admin-filter-failed',
                    'admin-filter'
                ),
                (
                    '50000000-0000-0000-0000-000000000002',
                    '40000000-0000-0000-0000-000000000001',
                    'Mail',
                    'DailyDigest',
                    'Pending',
                    0,
                    '2026-07-03T01:00:00Z',
                    null,
                    null,
                    'admin-filter-pending',
                    'admin-filter'
                );
            """);

        var failed = await adminClient.GetAsync("/api/admin/notification-deliveries?status=Failed", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        using var body = await JsonDocument.ParseAsync(await failed.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("Failed", items[0].GetProperty("status").GetString());
        Assert.Equal("RecipientEmailMissing", items[0].GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task AdminDeliveryEndpoint_ReturnsDisplayContextAndHandlingFields()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync();
        using var adminClient = context.CreateClient("dev-admin");
        var workItemId = await context.InsertWorkItemAsync("ProductionPlanning", "ProductionPlanningPrimary", DevAdminUserId, "2026-07-06", "delivery-context");
        await context.ExecuteSqlAsync($"""
            with inserted_notification as (
                insert into notifications (
                    project_id, notification_type, severity, title, message, link_url, idempotency_key
                )
                values (
                    '{DemoProjectId}',
                    'Info',
                    'Info',
                    '알림 표시 테스트',
                    '알림 표시 테스트 상세 메시지입니다.',
                    '/projects/{DemoProjectId}',
                    'admin-delivery-context'
                )
                returning id
            )
            insert into notification_deliveries (
                notification_id, notification_recipient_id, recipient_user_id, project_id, work_item_id,
                channel, delivery_type, status, attempt_count, error_code, error_message,
                dedupe_key, group_key, admin_handling_status
            )
            select
                id,
                null,
                '{DevSalesUserId}',
                '{DemoProjectId}',
                '{workItemId}',
                'Mail',
                'WorkItemCreated',
                'Failed',
                1,
                'RecipientEmailMissing',
                '수신자 이메일이 없습니다.',
                'admin-delivery-context',
                'admin-delivery-context',
                'Open'
            from inserted_notification;
            """);

        var response = await adminClient.GetAsync("/api/admin/notification-deliveries?status=Failed&handlingStatus=Open", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("알림 표시 테스트", item.GetProperty("displayTitle").GetString());
        Assert.Equal("Dev Sales User", item.GetProperty("displayRecipient").GetString());
        Assert.Equal("Demo Project Alpha · DEMO-24001", item.GetProperty("displayProject").GetString());
        Assert.Equal("업무 배정 알림", item.GetProperty("deliveryTypeLabel").GetString());
        Assert.Equal("발송 실패", item.GetProperty("statusLabel").GetString());
        Assert.Equal("미처리", item.GetProperty("adminHandlingStatusLabel").GetString());
        Assert.Equal("생산계획·담당자", item.GetProperty("workflowStageName").GetString());
        Assert.Contains("수신자 이메일", item.GetProperty("actionGuide").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminDeliveryHandling_ExcludesAcknowledgedAndDismissedRowsFromDashboardCounts()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync();
        using var adminClient = context.CreateClient("dev-admin");
        await context.ExecuteSqlAsync("""
            insert into notification_deliveries (
                recipient_user_id, project_id, channel, delivery_type, status, attempt_count,
                next_attempt_at_utc, error_code, error_message, dedupe_key, group_key
            )
            values
                (
                    '50000000-0000-0000-0000-000000000002',
                    '40000000-0000-0000-0000-000000000001',
                    'Mail',
                    'DailyDigest',
                    'Failed',
                    1,
                    null,
                    'RecipientEmailMissing',
                    '수신자 이메일이 없습니다.',
                    'admin-handle-failed',
                    'admin-handle'
                ),
                (
                    '50000000-0000-0000-0000-000000000002',
                    '40000000-0000-0000-0000-000000000001',
                    'Mail',
                    'DailyDigest',
                    'Pending',
                    0,
                    '2026-07-03T01:00:00Z',
                    null,
                    null,
                    'admin-handle-pending',
                    'admin-handle'
                );
            """);
        var failedId = await context.ReadScalarAsync<Guid>("select id from notification_deliveries where dedupe_key = 'admin-handle-failed';");
        var pendingId = await context.ReadScalarAsync<Guid>("select id from notification_deliveries where dedupe_key = 'admin-handle-pending';");

        var before = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/dashboard", TestContext.Current.CancellationToken);
        Assert.Equal(1, before.GetProperty("failedDeliveryCount").GetInt32());
        Assert.Equal(1, before.GetProperty("pendingDeliveryCount").GetInt32());

        var acknowledge = await adminClient.PostAsJsonAsync(
            "/api/admin/notification-deliveries/acknowledge",
            new NotificationDeliveryAdminActionRequest([failedId], "확인했습니다."),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, acknowledge.StatusCode);

        var dismiss = await adminClient.PostAsJsonAsync(
            "/api/admin/notification-deliveries/dismiss",
            new NotificationDeliveryAdminActionRequest([pendingId], "목록에서 제외합니다."),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, dismiss.StatusCode);

        var after = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/dashboard", TestContext.Current.CancellationToken);
        Assert.Equal(0, after.GetProperty("failedDeliveryCount").GetInt32());
        Assert.Equal(0, after.GetProperty("pendingDeliveryCount").GetInt32());
        Assert.Equal("Failed", await context.ReadScalarAsync<string>("select status from notification_deliveries where id = '" + failedId + "';"));
        Assert.Equal("Pending", await context.ReadScalarAsync<string>("select status from notification_deliveries where id = '" + pendingId + "';"));
        Assert.Equal("Acknowledged", await context.ReadScalarAsync<string>("select admin_handling_status from notification_deliveries where id = '" + failedId + "';"));
        Assert.Equal("Dismissed", await context.ReadScalarAsync<string>("select admin_handling_status from notification_deliveries where id = '" + pendingId + "';"));
    }

    [Fact]
    public async Task AdminDeliveryRetry_SchedulesPendingDeliveryForImmediateWorkerAttempt()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync();
        using var adminClient = context.CreateClient("dev-admin");
        await context.ExecuteSqlAsync("""
            insert into notification_deliveries (
                recipient_user_id, project_id, channel, delivery_type, status, attempt_count,
                next_attempt_at_utc, dedupe_key, group_key, admin_handling_status
            )
            values (
                '50000000-0000-0000-0000-000000000002',
                '40000000-0000-0000-0000-000000000001',
                'Mail',
                'DailyDigest',
                'Pending',
                2,
                '2026-07-04T00:00:00Z',
                'admin-retry-pending',
                'admin-retry',
                'Acknowledged'
            );
            """);
        var deliveryId = await context.ReadScalarAsync<Guid>("select id from notification_deliveries where dedupe_key = 'admin-retry-pending';");

        var retry = await adminClient.PostAsJsonAsync(
            "/api/admin/notification-deliveries/retry",
            new NotificationDeliveryAdminActionRequest([deliveryId], "지금 재시도합니다."),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("Open", await context.ReadScalarAsync<string>("select admin_handling_status from notification_deliveries where id = '" + deliveryId + "';"));
        Assert.Equal(2, await context.ReadScalarAsync<int>("select attempt_count from notification_deliveries where id = '" + deliveryId + "';"));
        var nextAttemptAt = await context.ReadScalarAsync<DateTime>("select next_attempt_at_utc from notification_deliveries where id = '" + deliveryId + "';");
        Assert.True(nextAttemptAt <= new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task AdminManualSendEndpoint_CreatesThreeChannelDeliveriesWithDisplaySnapshots()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Teams:Enabled"] = "true",
            ["Notifications:Teams:DryRun"] = "true",
            ["Notifications:Teams:WebhookUrl"] = "https://example.invalid/webhook",
            ["Notifications:TeamsActivity:Enabled"] = "true",
            ["Notifications:TeamsActivity:DryRun"] = "true",
            ["Notifications:TeamsActivity:InstalledAppId"] = "installation-id",
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true",
            ["Notifications:Mail:Provider"] = "Smtp",
            ["Notifications:Mail:TestRecipientEmail"] = "recipient@example.test"
        });
        using var adminClient = context.CreateClient("dev-admin");
        using var salesClient = context.CreateClient("dev-sales");
        await context.ExecuteSqlAsync("""
            update qms_users
            set auth_provider = 'EntraId',
                entra_object_id = 'dev-admin-entra-object',
                email = 'admin@example.test'
            where id = '50000000-0000-0000-0000-000000000001';
            """);
        var request = new NotificationManualSendRequest(
            NotificationManualKinds.ProjectCreated,
            DemoProjectId,
            "Project",
            "[테스트] 프로젝트 생성 알림",
            "TASK-NOTIFY-003 통합 알림 테스트",
            "실제 업무 알림이 아닙니다.",
            [
                NotificationDeliveryChannels.TeamsChannel,
                NotificationDeliveryChannels.TeamsActivity,
                NotificationDeliveryChannels.Mail
            ],
            [DevAdminUserId],
            [DevAdminUserId],
            ["recipient@example.test"],
            null,
            null,
            null,
            null);

        var forbidden = await salesClient.PostAsJsonAsync("/api/admin/notification-deliveries/send-manual", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var ok = await adminClient.PostAsJsonAsync("/api/admin/notification-deliveries/send-manual", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var response = await ok.Content.ReadFromJsonAsync<NotificationManualSendResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        Assert.Matches("^[0-9A-F]{8}$", response.CorrelationId);
        Assert.Equal(4, response.Items.Count);
        Assert.Equal(4, response.RequestedCount);
        Assert.Equal(4, response.QueuedCount);
        Assert.All(response.Items, item => Assert.Equal("Queued", item.Status));

        var correlationId = response.CorrelationId;
        Assert.Equal(4L, await context.ReadScalarAsync<long>($"select count(*) from notification_deliveries where correlation_id = '{correlationId}';"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>($"select count(*) from notification_deliveries where correlation_id = '{correlationId}' and channel = 'Mail' and display_recipient_email = 'recipient@example.test';"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>($"select count(*) from notification_deliveries where correlation_id = '{correlationId}' and channel = 'TeamsChannel' and display_channel_target = 'Teams 채널';"));
        Assert.Equal(4L, await context.ReadScalarAsync<long>($"select count(*) from notification_deliveries where correlation_id = '{correlationId}' and manual_notification_kind = 'ProjectCreated' and display_title = '[테스트] 프로젝트 생성 알림' and status = 'Pending';"));

        var pending = await context.DeliveryStore.GetDueDeliveriesAsync(10, 3, TestContext.Current.CancellationToken);
        var mailDelivery = Assert.Single(pending, item => item.Channel == NotificationDeliveryChannels.Mail && item.DisplayRecipientEmail == "recipient@example.test");
        var mailMessage = await context.DeliveryStore.RenderMessageAsync(mailDelivery, TestContext.Current.CancellationToken);
        Assert.Equal("[프로젝트 생성 알림] [테스트] 프로젝트 생성 알림", mailMessage.Subject);
        Assert.DoesNotContain(correlationId, mailMessage.Subject, StringComparison.Ordinal);
        Assert.Contains("EMI 프로젝트 통합관리시스템 알림", mailMessage.Body, StringComparison.Ordinal);
        Assert.Contains("알림 유형: 프로젝트 생성 알림", mailMessage.Body, StringComparison.Ordinal);
        Assert.Contains("프로젝트명: Demo Project Alpha", mailMessage.Body, StringComparison.Ordinal);

        var summary = await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, summary.ProcessedDeliveryCount);
        Assert.Equal(4L, await context.ReadScalarAsync<long>($"select count(*) from notification_deliveries where correlation_id = '{correlationId}' and status = 'DryRunSent';"));

        var list = await adminClient.GetAsync("/api/admin/notification-deliveries?deliveryType=ManualTest", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var body = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        var items = body.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("correlationId").GetString() == correlationId)
            .ToArray();
        Assert.Equal(4, items.Length);
        Assert.All(items, item =>
        {
            Assert.Equal("[테스트] 프로젝트 생성 알림", item.GetProperty("displayTitle").GetString());
            Assert.Equal("프로젝트 생성 알림", item.GetProperty("manualNotificationKindLabel").GetString());
            Assert.NotEqual("수신자 미등록", item.GetProperty("displayRecipient").GetString());
        });
        Assert.Contains(items, item => item.GetProperty("channel").GetString() == "Mail" && item.GetProperty("recipientEmailMasked").GetString() == "r***@example.test");
        Assert.Contains(items, item => item.GetProperty("channel").GetString() == "TeamsChannel" && item.GetProperty("displayRecipient").GetString() == "Teams 채널");
    }

    [Fact]
    public async Task AdminTestMailEndpoint_IsSystemAdministratorOnly()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true",
            ["Notifications:Mail:Provider"] = "Smtp"
        });
        using var adminClient = context.CreateClient("dev-admin");
        using var salesClient = context.CreateClient("dev-sales");

        var request = new NotificationTestMailRequest(
            "recipient@example.test",
            "TASK-NOTIFY-001 Graph Mail 테스트",
            "테스트 메일입니다.",
            null,
            null);

        var forbidden = await salesClient.PostAsJsonAsync("/api/admin/notification-deliveries/test-mail", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var ok = await adminClient.PostAsJsonAsync("/api/admin/notification-deliveries/test-mail", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var response = await ok.Content.ReadFromJsonAsync<NotificationTestMailResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        Assert.Equal(NotificationDeliveryStatuses.DryRunSent, response.Status);
        Assert.Equal("Smtp", response.Provider);
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'Mail' and delivery_type = 'ManualTest' and status = 'DryRunSent';"));
    }

    [Fact]
    public async Task AdminTestMailEndpoint_UsesConfiguredTestRecipient_WhenRequestOmitsRecipient()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true",
            ["Notifications:Mail:Provider"] = "Smtp",
            ["Notifications:Mail:TestRecipientEmail"] = "recipient@example.test",
            ["Notifications:Mail:SenderAddress"] = "sender@example.test"
        });
        using var adminClient = context.CreateClient("dev-admin");

        var ok = await adminClient.PostAsJsonAsync(
            "/api/admin/notification-deliveries/test-mail",
            new NotificationTestMailRequest(null, "TASK-NOTIFY-001 Graph Mail 테스트", "테스트 메일입니다.", true, "trace"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var response = await ok.Content.ReadFromJsonAsync<NotificationTestMailResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        Assert.Equal(NotificationDeliveryStatuses.DryRunSent, response.Status);
        Assert.Equal("Smtp", response.Provider);
        Assert.Equal("TestRecipientEmail", response.RecipientSource);
        Assert.Equal("SenderAddress", response.SenderSource);
        Assert.Equal(1, response.RecipientCount);
        Assert.False(response.SenderEqualsRecipient);
        Assert.True(response.SaveToSentItems);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
        Assert.Equal("s***@example.test", response.SenderMasked);
        Assert.Equal("r***@example.test", response.RecipientMasked);
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'Mail' and delivery_type = 'ManualTest' and display_recipient_email = 'recipient@example.test';"));
    }

    [Fact]
    public async Task AdminTestTeamsActivityEndpoint_IsSystemAdministratorOnly()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:TeamsActivity:Enabled"] = "true",
            ["Notifications:TeamsActivity:DryRun"] = "true",
            ["Notifications:TeamsActivity:TopicWebUrl"] = "https://qms.example.test"
        });
        using var adminClient = context.CreateClient("dev-admin");
        using var salesClient = context.CreateClient("dev-sales");
        var request = new NotificationTestTeamsActivityRequest(
            DevAdminUserId,
            "workItemAssigned",
            "TASK-NOTIFY-003 Teams Activity 테스트",
            "테스트 알림입니다.",
            "/projects/40000000-0000-0000-0000-000000000001");

        var forbidden = await salesClient.PostAsJsonAsync("/api/admin/notification-deliveries/test-teams-activity", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var ok = await adminClient.PostAsJsonAsync("/api/admin/notification-deliveries/test-teams-activity", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var response = await ok.Content.ReadFromJsonAsync<NotificationTestTeamsActivityResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        Assert.Equal(NotificationDeliveryStatuses.DryRunSent, response.Status);
        Assert.Equal("DryRun", response.Provider);
        Assert.Equal("workItemAssigned", response.ActivityType);
        Assert.False(response.IsActualEligible);
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'TeamsActivity' and delivery_type = 'ManualTest' and status = 'DryRunSent';"));
    }

    [Fact]
    public async Task AdminTestTeamsActivityEndpoint_ReturnsBadRequest_WhenActivityTypeIsNotDeclared()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:TeamsActivity:Enabled"] = "true",
            ["Notifications:TeamsActivity:DryRun"] = "true"
        });
        using var adminClient = context.CreateClient("dev-admin");

        var badRequest = await adminClient.PostAsJsonAsync(
            "/api/admin/notification-deliveries/test-teams-activity",
            new NotificationTestTeamsActivityRequest(DevAdminUserId, "notInManifest", "제목", "본문", null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);
        Assert.Equal(0L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'TeamsActivity';"));
    }

    [Fact]
    public async Task AdminTestMailEndpoint_ReturnsBadRequest_WhenRecipientIsMissing()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true"
        });
        using var adminClient = context.CreateClient("dev-admin");

        var badRequest = await adminClient.PostAsJsonAsync(
            "/api/admin/notification-deliveries/test-mail",
            new NotificationTestMailRequest(null, "TASK-NOTIFY-001 Graph Mail 테스트", "테스트 메일입니다.", null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);
        Assert.Equal(0L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where delivery_type = 'ManualTest';"));
    }

    [Fact]
    public async Task AdminTestMailEndpoint_ReturnsBadRequest_WhenRecipientFormatIsInvalid()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Mail:Enabled"] = "true",
            ["Notifications:Mail:DryRun"] = "true",
            ["Notifications:Mail:TestRecipientEmail"] = "invalid-recipient"
        });
        using var adminClient = context.CreateClient("dev-admin");

        var badRequest = await adminClient.PostAsJsonAsync(
            "/api/admin/notification-deliveries/test-mail",
            new NotificationTestMailRequest(null, "TASK-NOTIFY-001 Graph Mail 테스트", "테스트 메일입니다.", null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);
        Assert.Equal(0L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where delivery_type = 'ManualTest';"));
    }

    [Fact]
    public async Task Dispatcher_DoesNotRetryManualTestMailDeliveries()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(
            new Dictionary<string, string?>
            {
                ["Notifications:Mail:Enabled"] = "true",
                ["Notifications:Mail:DryRun"] = "false",
                ["Notifications:Mail:Provider"] = "Graph"
            },
            services =>
            {
                var descriptors = services.Where(service => service.ServiceType == typeof(IMailClient)).ToList();
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<IMailClient, FailingMailClient>();
            });
        using var adminClient = context.CreateClient("dev-admin");

        var request = new NotificationTestMailRequest(
            "recipient@example.test",
            "TASK-NOTIFY-001 Graph Mail 실패 테스트",
            "실패 재시도 방지 테스트입니다.",
            null,
            null);
        var ok = await adminClient.PostAsJsonAsync("/api/admin/notification-deliveries/test-mail", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var summary = await context.Dispatcher.DispatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.ProcessedDeliveryCount);
        Assert.Equal(NotificationDeliveryStatuses.Failed, await context.ReadScalarAsync<string>("select status from notification_deliveries where delivery_type = 'ManualTest';"));
        Assert.Equal(2, await context.ReadScalarAsync<int>("select attempt_count from notification_deliveries where delivery_type = 'ManualTest';"));
        Assert.Equal("GraphMailSenderNotFound", await context.ReadScalarAsync<string>("select error_code from notification_deliveries where delivery_type = 'ManualTest';"));
    }

    [Fact]
    public async Task Escalation_CreatesL0InAppAndTeamsPersonalDryRun_OnPreviousBusinessDay()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Escalation:Enabled"] = "true",
            ["Notifications:Escalation:TeamsPersonalDryRun"] = "true",
            ["Notifications:Escalation:MailEnabled"] = "true"
        });
        await context.InsertWorkItemAsync("ProductionPlanning", "ProductionPlanningPrimary", DevAdminUserId, "2026-07-06", "l0-previous-business-day");

        var summary = await context.Escalations.EvaluateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.EvaluatedWorkItemCount);
        Assert.Equal(1, summary.CreatedNotificationCount);
        Assert.Equal(1, summary.CreatedDeliveryCount);
        Assert.Equal("L0", await context.ReadScalarAsync<string>("select current_level from work_item_escalations;"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_recipients where user_id = '50000000-0000-0000-0000-000000000001';"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("""
            select count(*)
            from notification_deliveries
            where delivery_type = 'DueSoonL0'
              and channel = 'TeamsDirectMessage'
              and status = 'DryRunSent';
            """));
    }

    [Fact]
    public async Task Escalation_CanCreateTeamsActivityPersonalDelivery_WhenStrategyIsTeamsActivity()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Escalation:Enabled"] = "true",
            ["Notifications:Escalation:TeamsPersonalDryRun"] = "true",
            ["Notifications:Escalation:TeamsPersonalChannelStrategy"] = "TeamsActivity",
            ["Notifications:TeamsActivity:Enabled"] = "true",
            ["Notifications:TeamsActivity:DryRun"] = "true",
            ["Notifications:TeamsActivity:TopicWebUrl"] = "https://qms.example.test"
        });
        await context.InsertWorkItemAsync("ProductionPlanning", "ProductionPlanningPrimary", DevAdminUserId, "2026-07-06", "l0-teams-activity-strategy");

        var summary = await context.Escalations.EvaluateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.CreatedDeliveryCount);
        Assert.Equal(1L, await context.ReadScalarAsync<long>("""
            select count(*)
            from notification_deliveries
            where delivery_type = 'DueSoonL0'
              and channel = 'TeamsActivity'
              and status = 'Pending';
            """));
        Assert.Equal(0L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'TeamsDirectMessage';"));

        var delivery = Assert.Single(
            await context.DeliveryStore.GetDueDeliveriesAsync(10, 3, TestContext.Current.CancellationToken),
            item => item.Channel == NotificationDeliveryChannels.TeamsActivity);
        var message = await context.DeliveryStore.RenderMessageAsync(delivery, TestContext.Current.CancellationToken);
        var rendered = TeamsActivityNotificationRenderer.Render(
            message,
            context.NotificationOptions.CurrentValue.TeamsActivity);

        Assert.StartsWith("예정일 임박 알림, 예정일 임박:", message.Subject, StringComparison.Ordinal);
        Assert.Contains("예정일의 직전 영업일입니다.", message.Body, StringComparison.Ordinal);
        Assert.Equal("deadlineApproaching", rendered.ActivityType);
        Assert.Equal(message.Body, rendered.PreviewText);
        Assert.True(rendered.PreviewText.Length <= 150);
        Assert.DoesNotContain("Correlation", rendered.PreviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1L, await context.ReadScalarAsync<long>("""
            select count(*)
            from notification_deliveries
            where channel = 'TeamsActivity'
              and delivery_type = 'DueSoonL0'
              and display_work_item_title like '에스컬레이션 테스트 업무%'
              and display_recipient_name = 'Dev System Administrator';
            """));
    }

    [Fact]
    public async Task Escalation_CreatesL1MailAndTeamsPersonalDryRun_WhenOverdue()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Escalation:Enabled"] = "true",
            ["Notifications:Escalation:TeamsPersonalDryRun"] = "true",
            ["Notifications:Escalation:MailEnabled"] = "true"
        });
        await context.InsertWorkItemAsync("ProductionPlanning", "ProductionPlanningPrimary", DevAdminUserId, "2026-07-02", "l1-overdue");

        var summary = await context.Escalations.EvaluateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.CreatedNotificationCount);
        Assert.Equal(2, summary.CreatedDeliveryCount);
        Assert.Equal("L1", await context.ReadScalarAsync<string>("select current_level from work_item_escalations;"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("""
            select count(*)
            from notification_deliveries
            where delivery_type = 'OverdueL1'
              and channel = 'Mail'
              and status = 'Pending';
            """));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("""
            select count(*)
            from notification_deliveries
            where delivery_type = 'OverdueL1'
              and channel = 'TeamsDirectMessage'
              and status = 'DryRunSent';
            """));
    }

    [Fact]
    public async Task Escalation_CreatesL2ForSecondaryAndProductionPlanningWithoutTeamsChannelFallback()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Escalation:Enabled"] = "true",
            ["Notifications:Escalation:TeamsPersonalDryRun"] = "true",
            ["Notifications:Escalation:UseTeamsChannelFallback"] = "false"
        });
        await context.UpsertProjectAssigneeAsync("ProcurementSecondary", DevProcurementUserId);
        await context.UpsertProjectAssigneeAsync("ProductionPlanningPrimary", DevProductionUserId);
        await context.InsertWorkItemAsync("ProcurementInfo", "ProcurementPrimary", DevAdminUserId, "2026-07-01", "l2-recipients");

        var summary = await context.Escalations.EvaluateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.CreatedNotificationCount);
        Assert.Equal(2, summary.CreatedDeliveryCount);
        Assert.Equal("L2", await context.ReadScalarAsync<string>("select current_level from work_item_escalations;"));
        Assert.Equal(2L, await context.ReadScalarAsync<long>("select count(*) from notification_recipients;"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_recipients where user_id = '50000000-0000-0000-0000-000000000011';"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_recipients where user_id = '50000000-0000-0000-0000-000000000003';"));
        Assert.Equal(0L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where channel = 'TeamsChannel';"));
        Assert.Equal(2L, await context.ReadScalarAsync<long>("""
            select count(*)
            from notification_deliveries
            where delivery_type = 'OverdueL2'
              and channel = 'TeamsDirectMessage'
              and status = 'DryRunSent';
            """));
    }

    [Fact]
    public async Task Escalation_CreatesL3MailForProductionPlanningAndSalesOnly()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Escalation:Enabled"] = "true",
            ["Notifications:Escalation:TeamsPersonalDryRun"] = "true",
            ["Notifications:Escalation:MailEnabled"] = "true"
        });
        await context.UpsertProjectAssigneeAsync("ProductionPlanningPrimary", DevProductionUserId);
        await context.UpsertProjectAssigneeAsync("SalesPrimary", DevSalesUserId);
        await context.UpsertProjectAssigneeAsync("ProcurementSecondary", DevProcurementUserId);
        await context.InsertWorkItemAsync("ProcurementInfo", "ProcurementPrimary", DevAdminUserId, "2026-06-30", "l3-recipients");

        var summary = await context.Escalations.EvaluateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.CreatedNotificationCount);
        Assert.Equal(2, summary.CreatedDeliveryCount);
        Assert.Equal("L3", await context.ReadScalarAsync<string>("select current_level from work_item_escalations;"));
        Assert.Equal(2L, await context.ReadScalarAsync<long>("select count(*) from notification_recipients;"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_recipients where user_id = '50000000-0000-0000-0000-000000000003';"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_recipients where user_id = '50000000-0000-0000-0000-000000000002';"));
        Assert.Equal(0L, await context.ReadScalarAsync<long>("select count(*) from notification_recipients where user_id = '50000000-0000-0000-0000-000000000011';"));
        Assert.Equal(2L, await context.ReadScalarAsync<long>("""
            select count(*)
            from notification_deliveries
            where delivery_type = 'OverdueL3'
              and channel = 'Mail'
              and status = 'Pending';
            """));
        Assert.Equal(0L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where delivery_type = 'UrgentBlocking';"));
    }

    [Fact]
    public async Task Escalation_ResolvesCompletedWorkItemsAndDoesNotDuplicateSameLevel()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Escalation:Enabled"] = "true",
            ["Notifications:Escalation:TeamsPersonalDryRun"] = "true"
        });
        var workItemId = await context.InsertWorkItemAsync("ProductionPlanning", "ProductionPlanningPrimary", DevAdminUserId, "2026-07-06", "resolve-completed");

        await context.Escalations.EvaluateAsync(TestContext.Current.CancellationToken);
        await context.Escalations.EvaluateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1L, await context.ReadScalarAsync<long>("select count(*) from notification_deliveries where delivery_type = 'DueSoonL0';"));

        await context.ExecuteSqlAsync($"""
            update work_items
            set status = 'Completed',
                completed_at_utc = '2026-07-03T00:30:00Z'
            where id = '{workItemId}';
            """);

        var summary = await context.Escalations.EvaluateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.ResolvedEscalationCount);
        Assert.Equal("Resolved", await context.ReadScalarAsync<string>("select status from work_item_escalations;"));
        Assert.False(await context.ReadScalarAsync<bool>("select resolved_at_utc is null from work_item_escalations;"));
    }

    [Fact]
    public async Task AdminEscalationEndpoint_IsSystemAdministratorOnly()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync();
        using var adminClient = context.CreateClient("dev-admin");
        using var salesClient = context.CreateClient("dev-sales");

        var forbidden = await salesClient.GetAsync("/api/admin/work-item-escalations", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var ok = await adminClient.GetAsync("/api/admin/work-item-escalations", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        using var body = await JsonDocument.ParseAsync(await ok.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(body.RootElement.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task AdminEscalationEndpoint_FiltersByStatusAndDashboardBreaksDownActiveLevels()
    {
        await using var context = await NotificationDeliveryTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Notifications:Escalation:Enabled"] = "true",
            ["Notifications:Escalation:TeamsPersonalDryRun"] = "true"
        });
        using var adminClient = context.CreateClient("dev-admin");
        await context.InsertWorkItemAsync("ProductionPlanning", "ProductionPlanningPrimary", DevAdminUserId, "2026-07-02", "admin-filter-l1");
        await context.Escalations.EvaluateAsync(TestContext.Current.CancellationToken);

        var l1 = await adminClient.GetAsync("/api/admin/work-item-escalations?status=Active&level=L1", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, l1.StatusCode);
        using (var body = await JsonDocument.ParseAsync(await l1.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken))
        {
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            Assert.Equal("Active", items[0].GetProperty("status").GetString());
            Assert.Equal("L1", items[0].GetProperty("currentLevel").GetString());
        }

        var l2 = await adminClient.GetAsync("/api/admin/work-item-escalations?status=Active&level=L2", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, l2.StatusCode);
        using (var body = await JsonDocument.ParseAsync(await l2.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
        }

        var dashboard = await adminClient.GetAsync("/api/admin/dashboard", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        using (var body = await JsonDocument.ParseAsync(await dashboard.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.Equal(1, body.RootElement.GetProperty("activeEscalationCount").GetInt32());
            var levels = body.RootElement.GetProperty("activeEscalationLevels").EnumerateArray().ToArray();
            Assert.Equal(4, levels.Length);
            Assert.Contains(levels, item => item.GetProperty("level").GetString() == "L1" && item.GetProperty("count").GetInt32() == 1);
            Assert.Contains(levels, item => item.GetProperty("level").GetString() == "L0" && item.GetProperty("count").GetInt32() == 0);
        }
    }

    private static MailDeliveryPayload CreateMailPayload()
    {
        return new MailDeliveryPayload(
            "recipient@example.test",
            "메일 테스트",
            "메일 테스트 본문입니다.",
            null,
            "sender@example.test",
            CorrelationId: "ABC123");
    }

    private static NotificationDeliveryMessage CreateMailDeliveryMessage()
    {
        return new NotificationDeliveryMessage(
            Guid.NewGuid(),
            NotificationDeliveryChannels.Mail,
            NotificationDeliveryTypes.ManualTest,
            "메일 테스트",
            "메일 테스트 본문입니다.",
            null,
            "테스트 수신자",
            "recipient@example.test",
            CorrelationId: "ABC123");
    }

    private static NotificationDeliveryMessage CreateTeamsActivityDeliveryMessage(
        string authProvider = "EntraId",
        string? entraObjectId = "entra-user-object-id")
    {
        return new NotificationDeliveryMessage(
            Guid.NewGuid(),
            NotificationDeliveryChannels.TeamsActivity,
            NotificationDeliveryTypes.WorkItemCreated,
            "테스트 업무",
            "테스트 업무가 배정되었습니다.",
            "/projects/40000000-0000-0000-0000-000000000001",
            "테스트 수신자",
            "recipient@example.test",
            CorrelationId: "ABC123",
            RecipientUserId: DevAdminUserId,
            RecipientEntraObjectId: entraObjectId,
            RecipientAuthProvider: authProvider,
            RecipientUserIsActive: true);
    }

    private sealed class NotificationDeliveryTestContext : IAsyncDisposable
    {
        private NotificationDeliveryTestContext(PostgreSqlTestDatabase database, QmsWebApplicationFactory factory)
        {
            Database = database;
            Factory = factory;
            _ = Factory.CreateClient();
        }

        private PostgreSqlTestDatabase Database { get; }
        private QmsWebApplicationFactory Factory { get; }

        public NotificationDispatcher Dispatcher => Factory.Services.GetRequiredService<NotificationDispatcher>();

        public NotificationDeliveryStore DeliveryStore => Factory.Services.GetRequiredService<NotificationDeliveryStore>();

        public IOptionsMonitor<NotificationOptions> NotificationOptions => Factory.Services.GetRequiredService<IOptionsMonitor<NotificationOptions>>();

        public NotificationEscalationService Escalations => Factory.Services.GetRequiredService<NotificationEscalationService>();

        public static async Task<NotificationDeliveryTestContext> CreateAsync(
            IReadOnlyDictionary<string, string?>? overrides = null,
            Action<IServiceCollection>? configureTestServices = null)
        {
            var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
            var configuration = database.CreateConfiguration(new Dictionary<string, string?>
            {
                ["DevAuthentication:Enabled"] = "true",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["DevelopmentData:SeedEnabled"] = "true",
                ["Notifications:Dispatch:Enabled"] = "false",
                ["Notifications:DailyDigest:Enabled"] = "false",
                ["Notifications:Escalation:Enabled"] = "false",
                ["Notifications:Teams:Enabled"] = "false",
                ["Notifications:Teams:DryRun"] = "true",
                ["Notifications:TeamsActivity:Enabled"] = "false",
                ["Notifications:TeamsActivity:DryRun"] = "true",
                ["Notifications:Mail:Enabled"] = "false",
                ["Notifications:Mail:DryRun"] = "true"
            });
            var values = configuration.AsEnumerable()
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

            if (overrides is not null)
            {
                foreach (var item in overrides)
                {
                    values[item.Key] = item.Value;
                }
            }

            var factory = QmsWebApplicationFactory.Create(
                "Testing",
                values,
                includeDefaultDevelopmentAuthentication: true,
                configureTestServices: services =>
                {
                    var timeProviderDescriptor = services.Single(service => service.ServiceType == typeof(TimeProvider));
                    services.Remove(timeProviderDescriptor);
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero)));
                    configureTestServices?.Invoke(services);
                });

            return new NotificationDeliveryTestContext(database, factory);
        }

        public HttpClient CreateClient(string developmentUserKey)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUserKey);
            return client;
        }

        public async Task InsertNotificationAsync(
            string idempotencyKey,
            string notificationType,
            string severity,
            string title,
            string message,
            Guid recipientUserId)
        {
            await ExecuteSqlAsync($"""
                with inserted_notification as (
                    insert into notifications (
                        project_id, notification_type, severity, title, message, link_url, idempotency_key
                    )
                    values (
                        '{DemoProjectId}',
                        '{notificationType}',
                        '{severity}',
                        '{title}',
                        '{message}',
                        '/projects/{DemoProjectId}',
                        '{idempotencyKey}'
                    )
                    returning id
                )
                insert into notification_recipients (notification_id, user_id)
                select id, '{recipientUserId}'
                from inserted_notification;
                """);
        }

        public Task ExecuteSqlAsync(string sql)
        {
            return Database.ExecuteSqlAsync(sql, TestContext.Current.CancellationToken);
        }

        public Task<T> ReadScalarAsync<T>(string sql)
        {
            return Database.ReadScalarAsync<T>(sql, TestContext.Current.CancellationToken);
        }

        public Task ResetNextAttemptAsync(string channel)
        {
            return ExecuteSqlAsync($"""
                update notification_deliveries
                set next_attempt_at_utc = '2026-07-03T00:00:00Z'
                where channel = '{channel}';
                """);
        }

        public async Task<Guid> InsertWorkItemAsync(
            string workflowStageCode,
            string responsibilityType,
            Guid assignedUserId,
            string dueDate,
            string keySuffix)
        {
            var workItemId = Guid.NewGuid();
            await ExecuteSqlAsync($"""
                insert into work_items (
                    id, project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                    assigned_user_id, assigned_role_code, title, description, status, priority,
                    due_date, idempotency_key, created_by_user_id
                )
                values (
                    '{workItemId}',
                    '{DemoProjectId}',
                    'Project',
                    '{DemoProjectId}',
                    '{workflowStageCode}',
                    '{responsibilityType}',
                    '{assignedUserId}',
                    'system-administrator',
                    '에스컬레이션 테스트 업무 {keySuffix}',
                    '에스컬레이션 테스트입니다.',
                    'Requested',
                    'Normal',
                    '{dueDate}',
                    'notify-002-{keySuffix}-{workItemId}',
                    '{DevAdminUserId}'
                );
                """);

            return workItemId;
        }

        public Task UpsertProjectAssigneeAsync(string responsibilityType, Guid assignedUserId)
        {
            return ExecuteSqlAsync($"""
                insert into project_assignees (
                    project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc, note
                )
                values (
                    '{DemoProjectId}',
                    '{responsibilityType}',
                    '{assignedUserId}',
                    '{DevAdminUserId}',
                    '2026-07-03T00:00:00Z',
                    '에스컬레이션 테스트 담당자'
                )
                on conflict (project_id, responsibility_type) do update
                set assigned_user_id = excluded.assigned_user_id,
                    assigned_by_user_id = excluded.assigned_by_user_id,
                    assigned_at_utc = excluded.assigned_at_utc,
                    note = excluded.note,
                    row_version = project_assignees.row_version + 1;
                """);
        }

        public async ValueTask DisposeAsync()
        {
            Factory.Dispose();
            await Database.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class FailingTeamsWebhookClient : ITeamsWebhookClient
    {
        public Task<string> PostAsync(string webhookUrl, TeamsWebhookPayload payload, CancellationToken cancellationToken)
        {
            return Task.FromResult("http:500");
        }
    }

    private sealed class CapturingHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? ContentType { get; private set; }
        public string? RequestBody { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? ClientRequestId { get; private set; }
        public string? ReturnClientRequestId { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            ClientRequestId = request.Headers.TryGetValues("client-request-id", out var clientRequestIds)
                ? clientRequestIds.FirstOrDefault()
                : null;
            ReturnClientRequestId = request.Headers.TryGetValues("return-client-request-id", out var returnClientRequestIds)
                ? returnClientRequestIds.FirstOrDefault()
                : null;
            ContentType = request.Content?.Headers.ContentType?.ToString();
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(statusCode);
            response.Headers.Add("request-id", "test-request-id");
            return response;
        }
    }

    private sealed class TeamsActivityHttpMessageHandler(HttpStatusCode activityStatusCode, string? activityErrorBody = null) : HttpMessageHandler
    {
        public Uri? TokenRequestUri { get; private set; }
        public string TokenRequestBody { get; private set; } = "";
        public Uri? ActivityRequestUri { get; private set; }
        public string? ActivityRequestBody { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? ClientRequestId { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.Contains("/oauth2/v2.0/token", StringComparison.Ordinal) == true)
            {
                TokenRequestUri = request.RequestUri;
                TokenRequestBody = request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        access_token = "placeholder-access-token",
                        expires_in = 3600
                    })
                };
            }

            ActivityRequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            ClientRequestId = request.Headers.TryGetValues("client-request-id", out var clientRequestIds)
                ? clientRequestIds.FirstOrDefault()
                : null;
            ActivityRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(activityStatusCode);
            response.Headers.Add("request-id", "teams-activity-request-id");
            if (!string.IsNullOrWhiteSpace(activityErrorBody))
            {
                response.Content = new StringContent(activityErrorBody, Encoding.UTF8, "application/json");
            }
            return response;
        }
    }

    private sealed class CapturingTeamsActivityClient : ITeamsActivityClient
    {
        public bool WasCalled { get; private set; }
        public TeamsActivitySendRequest? Request { get; private set; }

        public Task<NotificationChannelResult> SendAsync(TeamsActivitySendRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            Request = request;
            return Task.FromResult(NotificationChannelResult.Sent("teams-activity-test"));
        }
    }

    private sealed class GraphTokenHttpMessageHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    access_token = "placeholder-access-token",
                    expires_in = 3600
                })
            };
        }
    }

    private sealed class StubGraphTokenProvider(string accessToken) : IGraphTokenProvider
    {
        public Task<GraphAccessTokenResult> GetTokenAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new GraphAccessTokenResult(true, accessToken, null, null));
        }
    }

    private sealed class FailingMailClient : IMailClient
    {
        public Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken)
        {
            return Task.FromResult(NotificationChannelResult.Failed(
                "GraphMailSenderNotFound",
                "Graph sendMail 요청이 실패했습니다. HTTP 404"));
        }
    }

    private sealed class ThrowingMailClient : IMailClient
    {
        public Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Actual mail client should not be called.");
        }
    }

    private sealed class CapturingSmtpMailClient : ISmtpMailClient
    {
        public bool WasCalled { get; private set; }

        public Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(NotificationChannelResult.Sent("smtp-test"));
        }
    }

    private sealed class CapturingGraphMailClient : IGraphMailClient
    {
        public bool WasCalled { get; private set; }

        public Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(NotificationChannelResult.Sent("graph-test"));
        }
    }

    private sealed class CapturingSmtpMailTransport : ISmtpMailTransport
    {
        public SmtpMailSendRequest? Request { get; private set; }

        public Task<string> SendAsync(SmtpMailSendRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(string.IsNullOrWhiteSpace(request.CorrelationId)
                ? "smtp-sent"
                : $"smtp-sent;client-request-id={request.CorrelationId}");
        }
    }

    private sealed class ThrowingSmtpMailTransport(string errorCode) : ISmtpMailTransport
    {
        public Task<string> SendAsync(SmtpMailSendRequest request, CancellationToken cancellationToken)
        {
            throw new SmtpMailTransportException(errorCode, "SMTP 테스트 오류입니다.");
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name)
        {
            return value;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private PostgreSqlTestDatabase(string databaseName, IConfiguration baseConfiguration)
        {
            DatabaseName = databaseName;
            BaseConfiguration = baseConfiguration;
        }

        private string DatabaseName { get; }
        private IConfiguration BaseConfiguration { get; }

        public static async Task<PostgreSqlTestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var baseConfiguration = BuildBaseDatabaseConfiguration(repositoryRoot);
            var databaseName = $"emi_qms_test_{Guid.NewGuid():N}";
            var adminConnectionString = BuildConnectionString(baseConfiguration, "postgres");

            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"create database {QuoteIdentifier(databaseName)};");
            await command.ExecuteNonQueryAsync(cancellationToken);

            return new PostgreSqlTestDatabase(databaseName, baseConfiguration);
        }

        public IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> overrides)
        {
            var values = BaseConfiguration.AsEnumerable()
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            values["DATABASE_NAME"] = DatabaseName;

            foreach (var item in overrides)
            {
                values[item.Key] = item.Value;
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        public async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            await using var dataSource = NpgsqlDataSource.Create(BuildConnectionString(BaseConfiguration, DatabaseName));
            await using var command = dataSource.CreateCommand(sql);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<T> ReadScalarAsync<T>(string sql, CancellationToken cancellationToken)
        {
            await using var dataSource = NpgsqlDataSource.Create(BuildConnectionString(BaseConfiguration, DatabaseName));
            await using var command = dataSource.CreateCommand(sql);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Assert.IsType<T>(value);
        }

        public async ValueTask DisposeAsync()
        {
            var adminConnectionString = BuildConnectionString(BaseConfiguration, "postgres");
            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var terminate = dataSource.CreateCommand($"""
                select pg_terminate_backend(pid)
                from pg_stat_activity
                where datname = '{DatabaseName}';
                """);
            await terminate.ExecuteNonQueryAsync();
            await using var drop = dataSource.CreateCommand($"drop database if exists {QuoteIdentifier(DatabaseName)};");
            await drop.ExecuteNonQueryAsync();
        }

        private static IConfiguration BuildBaseDatabaseConfiguration(string repositoryRoot)
        {
            var values = LoadDotEnv(Path.Combine(repositoryRoot, ".env"));
            return TestConfigurationIsolation.BuildBaseDatabaseConfiguration(values);
        }

        private static Dictionary<string, string?> LoadDotEnv(string envPath)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(envPath))
            {
                return values;
            }

            foreach (var rawLine in File.ReadAllLines(envPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                values[parts[0].Trim()] = parts[1].Trim().Trim('"', '\'');
            }

            return values;
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "database", "migrations")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root.");
        }

        private static string BuildConnectionString(IConfiguration configuration, string databaseName)
        {
            var provider = new DatabaseConnectionStringProvider(configuration);
            var configured = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(configured));

            var builder = new NpgsqlConnectionStringBuilder(configured)
            {
                Database = databaseName,
                Pooling = false
            };

            return builder.ConnectionString;
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }
    }
}
