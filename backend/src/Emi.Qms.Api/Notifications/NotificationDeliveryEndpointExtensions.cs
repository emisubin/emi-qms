using Emi.Qms.Api.Authorization;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Security.Claims;

namespace Emi.Qms.Api.Notifications;

public static class NotificationDeliveryEndpointExtensions
{
    public static IEndpointRouteBuilder MapNotificationDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/notification-deliveries", async (
            string? status,
            string? channel,
            string? deliveryType,
            string? handlingStatus,
            NotificationDeliveryStore deliveryStore,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await deliveryStore.ListDeliveriesAsync(status, channel, deliveryType, handlingStatus, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ListNotificationDeliveries");

        app.MapPost("/api/admin/notification-deliveries/acknowledge", async (
            NotificationDeliveryAdminActionRequest request,
            ClaimsPrincipal user,
            NotificationDeliveryStore deliveryStore,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await deliveryStore.HandleDeliveriesAsync(
                request.Ids,
                NotificationDeliveryAdminHandlingStatuses.Acknowledged,
                request.Note,
                currentUserId.Value,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("AcknowledgeNotificationDeliveries");

        app.MapPost("/api/admin/notification-deliveries/dismiss", async (
            NotificationDeliveryAdminActionRequest request,
            ClaimsPrincipal user,
            NotificationDeliveryStore deliveryStore,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await deliveryStore.HandleDeliveriesAsync(
                request.Ids,
                NotificationDeliveryAdminHandlingStatuses.Dismissed,
                request.Note,
                currentUserId.Value,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("DismissNotificationDeliveries");

        app.MapPost("/api/admin/notification-deliveries/retry", async (
            NotificationDeliveryAdminActionRequest request,
            ClaimsPrincipal user,
            NotificationDeliveryStore deliveryStore,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await deliveryStore.RetryPendingDeliveriesAsync(
                request.Ids,
                request.Note,
                currentUserId.Value,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("RetryNotificationDeliveries");

        app.MapPost("/api/admin/notification-deliveries/send-manual", async (
            NotificationManualSendRequest request,
            ClaimsPrincipal user,
            NotificationDeliveryStore deliveryStore,
            IOptionsMonitor<NotificationOptions> options,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var channels = NormalizeManualChannels(request.Channels);
            if (channels.Count == 0)
            {
                return Results.BadRequest(new { message = "발송할 채널을 하나 이상 선택해 주세요." });
            }

            var notificationKind = NormalizeManualKind(request.NotificationKind);
            var kindLabel = ManualKindLabel(notificationKind);
            var correlationId = CreateCorrelationId();
            var title = request.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return Results.BadRequest(new { message = "제목을 입력해 주세요." });
            }

            var message = request.Message?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return Results.BadRequest(new { message = "내용을 입력해 주세요." });
            }

            Guid? projectId = null;
            string? projectName = null;
            if (request.ProjectId is { } requestedProjectId)
            {
                var project = await deliveryStore.GetProjectSnapshotAsync(requestedProjectId, cancellationToken);
                if (project is null)
                {
                    return Results.BadRequest(new { message = "선택한 프로젝트를 찾을 수 없습니다." });
                }

                projectId = project.ProjectId;
                projectName = project.ProjectTitle;
            }
            else
            {
                projectName = string.IsNullOrWhiteSpace(request.ProjectName)
                    ? "기타"
                    : request.ProjectName.Trim();
            }

            var mailUserIds = NormalizeIds(request.MailRecipientUserIds, request.MailRecipientUserId);
            var teamsActivityUserIds = NormalizeIds(request.TeamsActivityRecipientUserIds, request.TeamsActivityRecipientUserId);
            var mailEmails = NormalizeEmails(request.MailRecipientEmails, request.MailRecipientEmail);

            if (channels.Contains(NotificationDeliveryChannels.TeamsActivity) && teamsActivityUserIds.Count == 0)
            {
                return Results.BadRequest(new { message = "Teams Activity 수신자를 한 명 이상 선택해 주세요." });
            }

            if (channels.Contains(NotificationDeliveryChannels.Mail)
                && mailUserIds.Count == 0
                && mailEmails.Count == 0)
            {
                return Results.BadRequest(new { message = "메일 수신자를 한 명 이상 선택하거나 이메일을 입력해 주세요." });
            }

            foreach (var email in mailEmails)
            {
                if (!IsValidEmail(email))
                {
                    return Results.BadRequest(new { message = "메일 수신자 이메일 형식이 올바르지 않습니다." });
                }
            }

            var notificationOptions = options.CurrentValue;
            if (channels.Contains(NotificationDeliveryChannels.TeamsChannel)
                && (!notificationOptions.Teams.Enabled || string.IsNullOrWhiteSpace(notificationOptions.Teams.WebhookUrl)))
            {
                return Results.BadRequest(new { message = "Teams 채널 발송 설정이 완료되지 않았습니다." });
            }

            var requestedAtUtc = DateTimeOffset.UtcNow;
            var payload = new NotificationManualPayload(
                notificationKind,
                kindLabel,
                title,
                projectName,
                message,
                requestedAtUtc);
            var results = new List<NotificationManualSendChannelResponse>();

            if (channels.Contains(NotificationDeliveryChannels.TeamsChannel))
            {
                results.Add(await QueueManualDeliveryAsync(
                    deliveryStore,
                    NotificationDeliveryChannels.TeamsChannel,
                    null,
                    new NotificationDeliveryDisplaySnapshot(
                        title,
                        message,
                        projectName,
                        null,
                        null,
                        null,
                        NotificationDisplayRecipientKinds.TeamsChannel,
                        "Teams 채널",
                        notificationKind,
                        correlationId),
                    $"manual-send:{correlationId}:teams-channel",
                    "Teams 채널",
                    projectId,
                    payload,
                    currentUserId.Value,
                    cancellationToken));
            }

            if (channels.Contains(NotificationDeliveryChannels.TeamsActivity))
            {
                foreach (var recipientUserId in teamsActivityUserIds)
                {
                    var recipient = await deliveryStore.GetTeamsActivityRecipientAsync(recipientUserId, cancellationToken);
                    if (recipient is null)
                    {
                        results.Add(ManualSendFailed(NotificationDeliveryChannels.TeamsActivity, "TeamsActivityRecipientNotFound", "Teams Activity 수신자를 찾을 수 없습니다.", "Teams Activity Feed"));
                        continue;
                    }

                    if (!recipient.IsActive
                        || !string.Equals(recipient.AuthProvider, "EntraId", StringComparison.Ordinal)
                        || string.IsNullOrWhiteSpace(recipient.EntraObjectId))
                    {
                        results.Add(ManualSendFailed(NotificationDeliveryChannels.TeamsActivity, "TeamsActivityRecipientInvalid", "Teams Activity actual 발송은 활성 EntraId 사용자만 선택할 수 있습니다.", MaskRecipient(recipient)));
                        continue;
                    }

                    results.Add(await QueueManualDeliveryAsync(
                        deliveryStore,
                        NotificationDeliveryChannels.TeamsActivity,
                        recipient.UserId,
                        new NotificationDeliveryDisplaySnapshot(
                            title,
                            message,
                            projectName,
                            null,
                            recipient.DisplayName,
                            recipient.Email,
                            NotificationDisplayRecipientKinds.User,
                            "Teams Activity Feed",
                            notificationKind,
                            correlationId),
                        $"manual-send:{correlationId}:teams-activity:{recipient.UserId:N}",
                        MaskRecipient(recipient),
                        projectId,
                        payload,
                        currentUserId.Value,
                        cancellationToken));
                }
            }

            if (channels.Contains(NotificationDeliveryChannels.Mail))
            {
                foreach (var recipientUserId in mailUserIds)
                {
                    var recipient = await deliveryStore.GetTeamsActivityRecipientAsync(recipientUserId, cancellationToken);
                    if (recipient is null)
                    {
                        results.Add(ManualSendFailed(NotificationDeliveryChannels.Mail, "MailRecipientNotFound", "메일 수신자를 찾을 수 없습니다.", "메일 수신자 미등록"));
                        continue;
                    }

                    if (!recipient.IsActive || string.IsNullOrWhiteSpace(recipient.Email))
                    {
                        results.Add(ManualSendFailed(NotificationDeliveryChannels.Mail, "RecipientEmailMissing", "메일 수신자 이메일을 확인해 주세요.", MaskRecipient(recipient)));
                        continue;
                    }

                    results.Add(await QueueManualDeliveryAsync(
                        deliveryStore,
                        NotificationDeliveryChannels.Mail,
                        recipient.UserId,
                        new NotificationDeliveryDisplaySnapshot(
                            title,
                            message,
                            projectName,
                            null,
                            recipient.DisplayName,
                            recipient.Email,
                            NotificationDisplayRecipientKinds.User,
                            "Mail",
                            notificationKind,
                            correlationId),
                        $"manual-send:{correlationId}:mail:{recipient.UserId:N}",
                        $"{recipient.DisplayName} ({MaskAddress(recipient.Email)})",
                        projectId,
                        payload,
                        currentUserId.Value,
                        cancellationToken));
                }

                foreach (var recipientEmail in mailEmails)
                {
                    results.Add(await QueueManualDeliveryAsync(
                        deliveryStore,
                        NotificationDeliveryChannels.Mail,
                        null,
                        new NotificationDeliveryDisplaySnapshot(
                            title,
                            message,
                            projectName,
                            null,
                            null,
                            recipientEmail,
                            NotificationDisplayRecipientKinds.Email,
                            "Mail",
                            notificationKind,
                            correlationId),
                        $"manual-send:{correlationId}:mail:{recipientEmail.ToLowerInvariant()}",
                        MaskAddress(recipientEmail),
                        projectId,
                        payload,
                        currentUserId.Value,
                        cancellationToken));
                }
            }

            var queuedCount = results.Count(item => item.Status == "Queued");
            return Results.Ok(new NotificationManualSendResponse(
                correlationId,
                results.Count,
                queuedCount,
                results));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("SendManualNotificationDeliveries");

        app.MapGet("/api/admin/notification-deliveries/{id:guid}", async (
            Guid id,
            NotificationDeliveryStore deliveryStore,
            CancellationToken cancellationToken) =>
        {
            var detail = await deliveryStore.GetDeliveryDetailAsync(id, cancellationToken);
            return detail is null
                ? Results.NotFound(new { message = "알림 발송 이력을 찾을 수 없습니다." })
                : Results.Ok(detail);
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("GetNotificationDeliveryDetail");

        app.MapPost("/api/admin/notification-deliveries/test-mail", async (
            NotificationTestMailRequest request,
            NotificationDeliveryStore deliveryStore,
            IEnumerable<INotificationChannelHandler> channelHandlers,
            IOptionsMonitor<NotificationOptions> options,
            CancellationToken cancellationToken) =>
        {
            var recipientEmail = string.IsNullOrWhiteSpace(request.RecipientEmail)
                ? options.CurrentValue.Mail.TestRecipientEmail
                : request.RecipientEmail;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return Results.BadRequest(new { message = "테스트 수신자 이메일이 설정되지 않았습니다." });
            }

            if (!IsValidEmail(recipientEmail))
            {
                return Results.BadRequest(new { message = "수신자 이메일 형식이 올바르지 않습니다." });
            }

            var mailOptions = options.CurrentValue.Mail;
            var sender = !string.IsNullOrWhiteSpace(mailOptions.SenderUserId)
                ? mailOptions.SenderUserId
                : mailOptions.SenderAddress;
            var senderSource = !string.IsNullOrWhiteSpace(mailOptions.SenderUserId)
                ? "SenderUserId"
                : !string.IsNullOrWhiteSpace(mailOptions.SenderAddress)
                    ? "SenderAddress"
                    : "missing";
            var recipientSource = string.IsNullOrWhiteSpace(request.RecipientEmail)
                ? "TestRecipientEmail"
                : "Request";
            var correlationId = CreateCorrelationId();
            var subject = BuildSubject(request.Subject, request.SubjectSuffix, correlationId);
            var saveToSentItems = request.SaveToSentItems ?? mailOptions.SaveTestMailToSentItems;
            var mailHandler = channelHandlers.Single(handler => handler.Channel == NotificationDeliveryChannels.Mail);
            var deliveryId = await deliveryStore.CreateManualTestMailDeliveryAsync(
                new NotificationDeliveryDisplaySnapshot(
                    subject,
                    BuildTestMailBody(request.Message, correlationId),
                    null,
                    null,
                    "테스트 수신자",
                    recipientEmail.Trim(),
                    NotificationDisplayRecipientKinds.Email,
                    "메일 수신자",
                    InferManualKind(subject),
                    correlationId),
                cancellationToken);
            var result = await mailHandler.SendAsync(
                new NotificationDeliveryMessage(
                    deliveryId,
                    NotificationDeliveryChannels.Mail,
                    NotificationDeliveryTypes.ManualTest,
                    subject,
                    BuildTestMailBody(request.Message, correlationId),
                    null,
                    "테스트 수신자",
                    recipientEmail.Trim(),
                    saveToSentItems,
                    correlationId,
                    mailOptions.SenderUserId,
                    mailOptions.SenderAddress),
                cancellationToken);
            await deliveryStore.MarkDeliveryResultAsync(deliveryId, result, retryCount: 1, cancellationToken);

            return Results.Ok(new NotificationTestMailResponse(
                deliveryId,
                result.Status,
                result.ErrorCode,
                result.ErrorMessage,
                string.IsNullOrWhiteSpace(mailOptions.Provider) ? "DryRun" : mailOptions.Provider,
                correlationId,
                senderSource,
                MaskAddress(sender),
                recipientSource,
                MaskAddress(recipientEmail),
                1,
                !string.IsNullOrWhiteSpace(sender) && string.Equals(sender, recipientEmail, StringComparison.OrdinalIgnoreCase),
                saveToSentItems));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("SendNotificationTestMail");

        app.MapPost("/api/admin/notification-deliveries/test-teams-activity", async (
            NotificationTestTeamsActivityRequest request,
            NotificationDeliveryStore deliveryStore,
            IEnumerable<INotificationChannelHandler> channelHandlers,
            IOptionsMonitor<NotificationOptions> options,
            CancellationToken cancellationToken) =>
        {
            if (request.RecipientUserId is not { } recipientUserId)
            {
                return Results.BadRequest(new { message = "Teams Activity 테스트 수신자 user id를 입력해 주세요." });
            }

            var recipient = await deliveryStore.GetTeamsActivityRecipientAsync(recipientUserId, cancellationToken);
            if (recipient is null)
            {
                return Results.BadRequest(new { message = "Teams Activity 테스트 수신자를 찾을 수 없습니다." });
            }

            var teamsActivityOptions = options.CurrentValue.TeamsActivity;
            var activityType = string.IsNullOrWhiteSpace(request.ActivityType)
                ? teamsActivityOptions.ActivityTypes.WorkItemAssigned
                : request.ActivityType.Trim();
            if (!TeamsActivityNotificationRenderer.IsDeclaredActivityType(activityType, teamsActivityOptions.ActivityTypes))
            {
                return Results.BadRequest(new { message = "Teams 앱 manifest 설정에 없는 activityType입니다." });
            }

            var correlationId = CreateCorrelationId();
            var title = string.IsNullOrWhiteSpace(request.Title)
                ? "TASK-NOTIFY-003 Teams Activity 테스트"
                : request.Title.Trim();
            var message = string.IsNullOrWhiteSpace(request.Message)
                ? "EMI 프로젝트 통합관리시스템 Teams Activity Feed dry-run 테스트입니다. 실제 업무 알림이 아닙니다."
                : request.Message.Trim();
            var installedAppId = string.IsNullOrWhiteSpace(request.InstalledAppId)
                ? teamsActivityOptions.InstalledAppId
                : request.InstalledAppId;
            var topicEntityUrl = BuildInstalledAppEntityUrl(recipient.EntraObjectId, installedAppId);
            var teamsActivityHandler = channelHandlers.Single(handler => handler.Channel == NotificationDeliveryChannels.TeamsActivity);
            var deliveryId = await deliveryStore.CreateManualTestTeamsActivityDeliveryAsync(
                recipient.UserId,
                new NotificationDeliveryDisplaySnapshot(
                    title,
                    BuildTestTeamsActivityBody(message, activityType, correlationId),
                    null,
                    null,
                    recipient.DisplayName,
                    recipient.Email,
                    NotificationDisplayRecipientKinds.User,
                    "Teams Activity Feed",
                    InferManualKind(title),
                    correlationId),
                cancellationToken);
            var result = await teamsActivityHandler.SendAsync(
                new NotificationDeliveryMessage(
                    deliveryId,
                    NotificationDeliveryChannels.TeamsActivity,
                    NotificationDeliveryTypes.ManualTest,
                    title,
                    BuildTestTeamsActivityBody(message, activityType, correlationId),
                    request.LinkUrl,
                    recipient.DisplayName,
                    recipient.Email,
                    CorrelationId: correlationId,
                    RecipientUserId: recipient.UserId,
                    RecipientEntraObjectId: recipient.EntraObjectId,
                    RecipientAuthProvider: recipient.AuthProvider,
                    RecipientUserIsActive: recipient.IsActive,
                    TeamsActivityType: activityType,
                    TeamsActivityTopicSource: topicEntityUrl is null ? null : "entityUrl",
                    TeamsActivityTopicValue: topicEntityUrl),
                cancellationToken);
            await deliveryStore.MarkDeliveryResultAsync(deliveryId, result, retryCount: 1, cancellationToken);

            var isActualEligible = recipient.IsActive
                && string.Equals(recipient.AuthProvider, "EntraId", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(recipient.EntraObjectId);

            return Results.Ok(new NotificationTestTeamsActivityResponse(
                deliveryId,
                result.Status,
                result.ErrorCode,
                result.ErrorMessage,
                teamsActivityOptions.DryRun ? "DryRun" : "Graph",
                correlationId,
                activityType,
                "Request",
                MaskRecipient(recipient),
                teamsActivityOptions.DryRun,
                isActualEligible,
                result.ProviderMessageId));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("SendNotificationTestTeamsActivity");

        return app;
    }

    private static async Task<NotificationManualSendChannelResponse> QueueManualDeliveryAsync(
        NotificationDeliveryStore deliveryStore,
        string channel,
        Guid? recipientUserId,
        NotificationDeliveryDisplaySnapshot snapshot,
        string groupKey,
        string target,
        Guid? projectId,
        NotificationManualPayload manualPayload,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var deliveryId = await deliveryStore.CreateManualDeliveryAsync(
            channel,
            recipientUserId,
            snapshot,
            groupKey,
            cancellationToken,
            projectId,
            manualPayload,
            requestedByUserId);

        return new NotificationManualSendChannelResponse(
            channel,
            ChannelLabel(channel),
            deliveryId,
            "Queued",
            null,
            null,
            target,
            "발송 요청이 접수되었습니다.");
    }

    private static NotificationManualSendChannelResponse ManualSendFailed(
        string channel,
        string errorCode,
        string errorMessage,
        string target)
    {
        return new NotificationManualSendChannelResponse(
            channel,
            ChannelLabel(channel),
            null,
            "Failed",
            errorCode,
            errorMessage,
            target,
            "발송 요청을 접수하지 못했습니다.");
    }

    private static IReadOnlyList<Guid> NormalizeIds(IReadOnlyList<Guid>? ids, Guid? legacyId)
    {
        var values = new List<Guid>();
        if (ids is not null)
        {
            values.AddRange(ids.Where(id => id != Guid.Empty));
        }

        if (legacyId is { } id && id != Guid.Empty)
        {
            values.Add(id);
        }

        return values.Distinct().ToArray();
    }

    private static IReadOnlyList<string> NormalizeEmails(IReadOnlyList<string>? emails, string? legacyEmail)
    {
        var values = new List<string>();
        if (emails is not null)
        {
            values.AddRange(emails);
        }

        if (!string.IsNullOrWhiteSpace(legacyEmail))
        {
            values.Add(legacyEmail);
        }

        return values
            .SelectMany(value => value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<NotificationManualSendChannelResponse> SendManualChannelAsync(
        string channel,
        INotificationChannelHandler handler,
        NotificationManualSendRequest request,
        NotificationDeliveryStore deliveryStore,
        NotificationOptions options,
        string notificationKind,
        string kindLabel,
        string title,
        string? projectName,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return channel switch
            {
                NotificationDeliveryChannels.TeamsChannel => await SendManualTeamsChannelAsync(
                    handler,
                    deliveryStore,
                    notificationKind,
                    kindLabel,
                    title,
                    projectName,
                    message,
                    correlationId,
                    cancellationToken),
                NotificationDeliveryChannels.TeamsActivity => await SendManualTeamsActivityAsync(
                    handler,
                    request,
                    deliveryStore,
                    options.TeamsActivity,
                    notificationKind,
                    kindLabel,
                    title,
                    projectName,
                    message,
                    correlationId,
                    cancellationToken),
                NotificationDeliveryChannels.Mail => await SendManualMailAsync(
                    handler,
                    request,
                    deliveryStore,
                    options.Mail,
                    notificationKind,
                    kindLabel,
                    title,
                    projectName,
                    message,
                    correlationId,
                    cancellationToken),
                _ => new NotificationManualSendChannelResponse(
                    channel,
                    ChannelLabel(channel),
                    null,
                    "Failed",
                    "NotificationChannelUnsupported",
                    "지원하지 않는 알림 채널입니다.",
                    ChannelLabel(channel),
                    "발송하지 못했습니다.")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new NotificationManualSendChannelResponse(
                channel,
                ChannelLabel(channel),
                null,
                "Failed",
                "ManualNotificationSendFailed",
                "수동 알림 발송 중 오류가 발생했습니다.",
                ChannelLabel(channel),
                "발송하지 못했습니다.");
        }
    }

    private static async Task<NotificationManualSendChannelResponse> SendManualTeamsChannelAsync(
        INotificationChannelHandler handler,
        NotificationDeliveryStore deliveryStore,
        string notificationKind,
        string kindLabel,
        string title,
        string? projectName,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
    {
        const string target = "Teams 채널";
        var body = BuildManualBody(kindLabel, title, projectName, message, correlationId, target);
        var deliveryId = await deliveryStore.CreateManualDeliveryAsync(
            NotificationDeliveryChannels.TeamsChannel,
            null,
            new NotificationDeliveryDisplaySnapshot(
                title,
                body,
                projectName,
                null,
                null,
                null,
                NotificationDisplayRecipientKinds.TeamsChannel,
                target,
                notificationKind,
                correlationId),
            $"manual-send:{correlationId}:teams-channel",
            cancellationToken);
        var result = await handler.SendAsync(
            new NotificationDeliveryMessage(
                deliveryId,
                NotificationDeliveryChannels.TeamsChannel,
                NotificationDeliveryTypes.ManualTest,
                title,
                body,
                null,
                target,
                null,
                CorrelationId: correlationId),
            cancellationToken);
        await deliveryStore.MarkDeliveryResultAsync(deliveryId, result, retryCount: 1, cancellationToken);
        return ToManualSendChannelResponse(NotificationDeliveryChannels.TeamsChannel, deliveryId, result, target);
    }

    private static async Task<NotificationManualSendChannelResponse> SendManualTeamsActivityAsync(
        INotificationChannelHandler handler,
        NotificationManualSendRequest request,
        NotificationDeliveryStore deliveryStore,
        NotificationTeamsActivityOptions options,
        string notificationKind,
        string kindLabel,
        string title,
        string? projectName,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (request.TeamsActivityRecipientUserId is not { } recipientUserId)
        {
            return new NotificationManualSendChannelResponse(
                NotificationDeliveryChannels.TeamsActivity,
                ChannelLabel(NotificationDeliveryChannels.TeamsActivity),
                null,
                "Failed",
                "TeamsActivityRecipientMissing",
                "Teams Activity 수신자를 선택해 주세요.",
                "Teams Activity Feed",
                "발송하지 못했습니다.");
        }

        var recipient = await deliveryStore.GetTeamsActivityRecipientAsync(recipientUserId, cancellationToken);
        if (recipient is null)
        {
            return new NotificationManualSendChannelResponse(
                NotificationDeliveryChannels.TeamsActivity,
                ChannelLabel(NotificationDeliveryChannels.TeamsActivity),
                null,
                "Failed",
                "TeamsActivityRecipientNotFound",
                "Teams Activity 수신자를 찾을 수 없습니다.",
                "Teams Activity Feed",
                "발송하지 못했습니다.");
        }

        var activityType = ResolveManualTeamsActivityType(notificationKind, options.ActivityTypes);
        var topicEntityUrl = BuildInstalledAppEntityUrl(recipient.EntraObjectId, options.InstalledAppId);
        var body = BuildManualBody(kindLabel, title, projectName, message, correlationId, "Teams Activity Feed");
        var deliveryId = await deliveryStore.CreateManualDeliveryAsync(
            NotificationDeliveryChannels.TeamsActivity,
            recipient.UserId,
            new NotificationDeliveryDisplaySnapshot(
                title,
                body,
                projectName,
                null,
                recipient.DisplayName,
                recipient.Email,
                NotificationDisplayRecipientKinds.User,
                "Teams Activity Feed",
                notificationKind,
                correlationId),
            $"manual-send:{correlationId}:teams-activity",
            cancellationToken);
        var result = await handler.SendAsync(
            new NotificationDeliveryMessage(
                deliveryId,
                NotificationDeliveryChannels.TeamsActivity,
                NotificationDeliveryTypes.ManualTest,
                title,
                body,
                null,
                recipient.DisplayName,
                recipient.Email,
                CorrelationId: correlationId,
                RecipientUserId: recipient.UserId,
                RecipientEntraObjectId: recipient.EntraObjectId,
                RecipientAuthProvider: recipient.AuthProvider,
                RecipientUserIsActive: recipient.IsActive,
                TeamsActivityType: activityType,
                TeamsActivityTopicSource: topicEntityUrl is null ? null : "entityUrl",
                TeamsActivityTopicValue: topicEntityUrl),
            cancellationToken);
        await deliveryStore.MarkDeliveryResultAsync(deliveryId, result, retryCount: 1, cancellationToken);
        return ToManualSendChannelResponse(NotificationDeliveryChannels.TeamsActivity, deliveryId, result, MaskRecipient(recipient));
    }

    private static async Task<NotificationManualSendChannelResponse> SendManualMailAsync(
        INotificationChannelHandler handler,
        NotificationManualSendRequest request,
        NotificationDeliveryStore deliveryStore,
        NotificationMailOptions options,
        string notificationKind,
        string kindLabel,
        string title,
        string? projectName,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
    {
        TeamsActivityRecipientProfile? recipient = null;
        if (request.MailRecipientUserId is { } userId)
        {
            recipient = await deliveryStore.GetTeamsActivityRecipientAsync(userId, cancellationToken);
        }

        var recipientEmail = !string.IsNullOrWhiteSpace(request.MailRecipientEmail)
            ? request.MailRecipientEmail.Trim()
            : !string.IsNullOrWhiteSpace(recipient?.Email)
                ? recipient.Email.Trim()
                : options.TestRecipientEmail?.Trim();
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return new NotificationManualSendChannelResponse(
                NotificationDeliveryChannels.Mail,
                ChannelLabel(NotificationDeliveryChannels.Mail),
                null,
                "Failed",
                "RecipientEmailMissing",
                "메일 수신자 이메일을 입력하거나 TestRecipientEmail을 설정해 주세요.",
                "메일 수신자 미등록",
                "발송하지 못했습니다.");
        }

        if (!IsValidEmail(recipientEmail))
        {
            return new NotificationManualSendChannelResponse(
                NotificationDeliveryChannels.Mail,
                ChannelLabel(NotificationDeliveryChannels.Mail),
                null,
                "Failed",
                "RecipientEmailInvalid",
                "메일 수신자 이메일 형식이 올바르지 않습니다.",
                MaskAddress(recipientEmail),
                "발송하지 못했습니다.");
        }

        var target = recipient is null
            ? MaskAddress(recipientEmail)
            : $"{recipient.DisplayName} ({MaskAddress(recipientEmail)})";
        var body = BuildManualBody(kindLabel, title, projectName, message, correlationId, "Mail");
        var deliveryId = await deliveryStore.CreateManualDeliveryAsync(
            NotificationDeliveryChannels.Mail,
            recipient?.UserId,
            new NotificationDeliveryDisplaySnapshot(
                title,
                body,
                projectName,
                null,
                recipient?.DisplayName,
                recipientEmail,
                recipient is null ? NotificationDisplayRecipientKinds.Email : NotificationDisplayRecipientKinds.User,
                "Mail",
                notificationKind,
                correlationId),
            $"manual-send:{correlationId}:mail",
            cancellationToken);
        var result = await handler.SendAsync(
            new NotificationDeliveryMessage(
                deliveryId,
                NotificationDeliveryChannels.Mail,
                NotificationDeliveryTypes.ManualTest,
                $"{title} [{correlationId}]",
                body,
                null,
                recipient?.DisplayName ?? "메일 수신자",
                recipientEmail,
                options.SaveTestMailToSentItems,
                correlationId,
                options.SenderUserId,
                options.SenderAddress,
                RecipientUserId: recipient?.UserId,
                RecipientEntraObjectId: recipient?.EntraObjectId,
                RecipientAuthProvider: recipient?.AuthProvider,
                RecipientUserIsActive: recipient?.IsActive),
            cancellationToken);
        await deliveryStore.MarkDeliveryResultAsync(deliveryId, result, retryCount: 1, cancellationToken);
        return ToManualSendChannelResponse(NotificationDeliveryChannels.Mail, deliveryId, result, target);
    }

    private static NotificationManualSendChannelResponse ToManualSendChannelResponse(
        string channel,
        Guid deliveryId,
        NotificationChannelResult result,
        string target)
    {
        return new NotificationManualSendChannelResponse(
            channel,
            ChannelLabel(channel),
            deliveryId,
            result.Status,
            result.ErrorCode,
            result.ErrorMessage,
            target,
            result.Status is NotificationDeliveryStatuses.Sent or NotificationDeliveryStatuses.DryRunSent
                ? "발송 처리됐습니다."
                : "발송 결과를 확인해 주세요.");
    }

    private static IReadOnlyList<string> NormalizeManualChannels(IReadOnlyList<string>? channels)
    {
        if (channels is null)
        {
            return [];
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            NotificationDeliveryChannels.TeamsChannel,
            NotificationDeliveryChannels.TeamsActivity,
            NotificationDeliveryChannels.Mail
        };
        return channels
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Select(channel => channel.Trim())
            .Where(allowed.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeManualKind(string? kind)
    {
        return kind?.Trim() switch
        {
            NotificationManualKinds.ProjectCreated => NotificationManualKinds.ProjectCreated,
            NotificationManualKinds.WorkItemAssigned => NotificationManualKinds.WorkItemAssigned,
            NotificationManualKinds.Urgent => NotificationManualKinds.Urgent,
            NotificationManualKinds.DailyDigest => NotificationManualKinds.DailyDigest,
            NotificationManualKinds.Custom => NotificationManualKinds.Custom,
            _ => NotificationManualKinds.Custom
        };
    }

    private static string InferManualKind(string value)
    {
        return value.Contains("프로젝트 생성", StringComparison.Ordinal)
            ? NotificationManualKinds.ProjectCreated
            : NotificationManualKinds.Custom;
    }

    private static string ManualKindLabel(string kind)
    {
        return kind switch
        {
            NotificationManualKinds.ProjectCreated => "프로젝트 생성 알림",
            NotificationManualKinds.WorkItemAssigned => "업무 배정 알림",
            NotificationManualKinds.Urgent => "긴급 알림",
            NotificationManualKinds.DailyDigest => "일일 업무 요약",
            _ => "일반 알림"
        };
    }

    private static string ResolveManualTeamsActivityType(string kind, NotificationTeamsActivityTypeOptions activityTypes)
    {
        return kind switch
        {
            NotificationManualKinds.Urgent => activityTypes.UrgentPending,
            NotificationManualKinds.DailyDigest => activityTypes.DailyDigest,
            NotificationManualKinds.WorkItemAssigned => activityTypes.WorkItemAssigned,
            NotificationManualKinds.ProjectCreated => activityTypes.WorkItemAssigned,
            _ => activityTypes.WorkItemAssigned
        };
    }

    private static string ChannelLabel(string channel)
    {
        return channel switch
        {
            NotificationDeliveryChannels.TeamsChannel => "Teams 채널",
            NotificationDeliveryChannels.TeamsActivity => "Teams Activity",
            NotificationDeliveryChannels.Mail => "메일",
            _ => channel
        };
    }

    private static string BuildManualBody(
        string kindLabel,
        string title,
        string? projectName,
        string message,
        string correlationId,
        string channelLabel)
    {
        var projectLine = string.IsNullOrWhiteSpace(projectName)
            ? ""
            : $"{Environment.NewLine}프로젝트명: {projectName}";
        return $"""
            EMI 프로젝트 통합관리시스템

            알림 유형: {kindLabel}
            제목: {title}{projectLine}

            {message}

            실제 업무 알림이 아닌 관리자 수동 발송 알림입니다.
            채널: {channelLabel}
            Correlation ID: {correlationId}
            발송 시각: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
            """;
    }

    private static string BuildSubject(string? subject, string? suffix, string correlationId)
    {
        var baseSubject = string.IsNullOrWhiteSpace(subject)
            ? "TASK-NOTIFY-001 Graph Mail 테스트"
            : subject.Trim();
        var suffixText = string.IsNullOrWhiteSpace(suffix)
            ? ""
            : $" {suffix.Trim()}";
        return $"{baseSubject}{suffixText} [{correlationId}]";
    }

    private static string BuildTestMailBody(string? message, string correlationId)
    {
        var body = string.IsNullOrWhiteSpace(message)
            ? "EMI 프로젝트 통합관리시스템 UAT 테스트 메일입니다. 실제 업무 알림이 아닙니다."
            : message.Trim();

        return $"""
            EMI 프로젝트 통합관리시스템

            {body}

            환경: UAT
            채널: Mail
            Correlation ID: {correlationId}
            발송 시각: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
            """;
    }

    private static string BuildTestTeamsActivityBody(string message, string activityType, string correlationId)
    {
        return $"""
            EMI 프로젝트 통합관리시스템

            {message}

            환경: UAT
            채널: TeamsActivity
            ActivityType: {activityType}
            Correlation ID: {correlationId}
            발송 시각: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
            """;
    }

    private static string? BuildInstalledAppEntityUrl(string? recipientEntraObjectId, string? installedAppId)
    {
        if (string.IsNullOrWhiteSpace(recipientEntraObjectId)
            || string.IsNullOrWhiteSpace(installedAppId))
        {
            return null;
        }

        return "https://graph.microsoft.com/v1.0/users/"
            + Uri.EscapeDataString(recipientEntraObjectId.Trim())
            + "/teamwork/installedApps/"
            + Uri.EscapeDataString(installedAppId.Trim());
    }

    private static string CreateCorrelationId()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
    }

    private static string MaskAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var at = value.LastIndexOf('@');
        if (at <= 0 || at == value.Length - 1)
        {
            return value.Length <= 4 ? "***" : value[..4] + "***";
        }

        return value[..1] + "***" + value[at..];
    }

    private static string MaskRecipient(TeamsActivityRecipientProfile recipient)
    {
        if (!string.IsNullOrWhiteSpace(recipient.Email))
        {
            return MaskAddress(recipient.Email);
        }

        if (!string.IsNullOrWhiteSpace(recipient.EntraObjectId))
        {
            return MaskIdentifier(recipient.EntraObjectId);
        }

        return MaskIdentifier(recipient.UserId.ToString("D"));
    }

    private static string MaskIdentifier(string value)
    {
        return value.Length <= 8 ? "***" : $"{value[..4]}***{value[^4..]}";
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        return Guid.TryParse(user.FindFirstValue(QmsClaimTypes.UserId), out var userId) ? userId : null;
    }
}
