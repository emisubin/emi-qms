using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public interface INotificationChannelHandler
{
    string Channel { get; }

    Task<NotificationChannelResult> SendAsync(NotificationDeliveryMessage message, CancellationToken cancellationToken);
}

public interface ITeamsWebhookClient
{
    Task<string> PostAsync(string webhookUrl, TeamsWebhookPayload payload, CancellationToken cancellationToken);
}

public sealed record TeamsWebhookPayload(
    [property: JsonPropertyName("$schema")] string Schema,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("body")] IReadOnlyList<IReadOnlyDictionary<string, object>> Body)
{
    public static TeamsWebhookPayload FromMessage(NotificationDeliveryMessage message)
    {
        var body = new List<IReadOnlyDictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                ["type"] = "TextBlock",
                ["text"] = TrimMessage(message.Subject, 300),
                ["weight"] = "Bolder",
                ["size"] = "Medium",
                ["wrap"] = true
            },
            new Dictionary<string, object>
            {
                ["type"] = "TextBlock",
                ["text"] = TrimMessage(message.Body, 24_000),
                ["wrap"] = true
            },
            new Dictionary<string, object>
            {
                ["type"] = "FactSet",
                ["facts"] = BuildFacts(message)
            }
        };

        return new TeamsWebhookPayload(
            "http://adaptivecards.io/schemas/adaptive-card.json",
            "AdaptiveCard",
            "1.4",
            body);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object>> BuildFacts(NotificationDeliveryMessage message)
    {
        var facts = new List<IReadOnlyDictionary<string, object>>
        {
            Fact("시스템", "EMI 프로젝트 통합관리시스템"),
            Fact("채널", message.Channel),
            Fact("유형", message.DeliveryType),
            Fact("발송 시각", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"))
        };

        if (!string.IsNullOrWhiteSpace(message.RecipientDisplayName))
        {
            facts.Add(Fact("수신자", message.RecipientDisplayName));
        }

        if (!string.IsNullOrWhiteSpace(message.LinkUrl))
        {
            facts.Add(Fact("링크", message.LinkUrl));
        }

        return facts;
    }

    private static IReadOnlyDictionary<string, object> Fact(string title, string value)
    {
        return new Dictionary<string, object>
        {
            ["title"] = title,
            ["value"] = TrimMessage(value, 1_000)
        };
    }

    private static string TrimMessage(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

public interface IMailClient
{
    Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken);
}

public sealed record MailDeliveryPayload(
    string RecipientEmail,
    string Subject,
    string Body,
    string? SenderUserId,
    string? SenderAddress,
    bool SaveToSentItems = false,
    string? CorrelationId = null);

public sealed class TeamsWebhookClient(HttpClient httpClient) : ITeamsWebhookClient
{
    public async Task<string> PostAsync(string webhookUrl, TeamsWebhookPayload payload, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return $"http:{(int)response.StatusCode}";
        }

        return response.Headers.TryGetValues("request-id", out var values)
            ? values.FirstOrDefault() ?? "teams-webhook"
            : "teams-webhook-accepted";
    }
}

public sealed class TeamsChannelHandler(
    IOptionsMonitor<NotificationOptions> options,
    ITeamsWebhookClient teamsWebhookClient,
    ILogger<TeamsChannelHandler> logger)
    : INotificationChannelHandler
{
    public string Channel => NotificationDeliveryChannels.TeamsChannel;

    public async Task<NotificationChannelResult> SendAsync(NotificationDeliveryMessage message, CancellationToken cancellationToken)
    {
        var teams = options.CurrentValue.Teams;
        if (!teams.Enabled)
        {
            return NotificationChannelResult.Disabled("TeamsDisabled", "Teams 통합 채널 발송이 비활성화되어 있습니다.");
        }

        if (teams.DryRun)
        {
            return NotificationChannelResult.DryRunSent();
        }

        if (!string.Equals(teams.PayloadMode, "AdaptiveCardRoot", StringComparison.OrdinalIgnoreCase))
        {
            return NotificationChannelResult.Disabled("TeamsPayloadModeUnsupported", "지원하지 않는 Teams payload mode입니다.");
        }

        if (string.IsNullOrWhiteSpace(teams.WebhookUrl))
        {
            return NotificationChannelResult.Disabled("TeamsWebhookUrlMissing", "Teams Webhook URL이 설정되지 않았습니다.");
        }

        try
        {
            var providerMessageId = await teamsWebhookClient.PostAsync(
                teams.WebhookUrl,
                TeamsWebhookPayload.FromMessage(message),
                cancellationToken);
            return providerMessageId.StartsWith("http:", StringComparison.Ordinal)
                ? NotificationChannelResult.Failed("TeamsWebhookFailed", "Teams Webhook 요청이 실패했습니다.")
                : NotificationChannelResult.Sent(providerMessageId);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Teams Webhook notification delivery failed.");
            return NotificationChannelResult.Failed("TeamsWebhookFailed", "Teams Webhook 요청이 실패했습니다.");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Teams Webhook notification delivery timed out.");
            return NotificationChannelResult.Failed("TeamsWebhookTimeout", "Teams Webhook 요청 시간이 초과되었습니다.");
        }
    }
}

public sealed class TeamsDirectMessageHandler(IOptionsMonitor<NotificationOptions> options)
    : INotificationChannelHandler
{
    public string Channel => NotificationDeliveryChannels.TeamsDirectMessage;

    public Task<NotificationChannelResult> SendAsync(NotificationDeliveryMessage message, CancellationToken cancellationToken)
    {
        var teams = options.CurrentValue.Teams;
        if (!teams.Enabled)
        {
            return Task.FromResult(NotificationChannelResult.Disabled("TeamsDisabled", "Teams DM 발송이 비활성화되어 있습니다."));
        }

        return Task.FromResult(teams.DryRun
            ? NotificationChannelResult.DryRunSent()
            : NotificationChannelResult.Disabled("TeamsDirectMessageGraphNotConfigured", "Teams DM Graph 연동은 후속 TASK에서 활성화합니다."));
    }
}

public sealed class TeamsActivityChannelHandler(
    IOptionsMonitor<NotificationOptions> options,
    ITeamsActivityClient teamsActivityClient)
    : INotificationChannelHandler
{
    public string Channel => NotificationDeliveryChannels.TeamsActivity;

    public Task<NotificationChannelResult> SendAsync(NotificationDeliveryMessage message, CancellationToken cancellationToken)
    {
        var teamsActivity = options.CurrentValue.TeamsActivity;
        if (!teamsActivity.Enabled)
        {
            return Task.FromResult(NotificationChannelResult.Disabled(
                "TeamsActivityDisabled",
                "Teams Activity Feed 발송이 비활성화되어 있습니다."));
        }

        if (message.RecipientUserIsActive == false)
        {
            return Task.FromResult(NotificationChannelResult.Suppressed(
                "TeamsActivityUserInactive",
                "비활성 사용자는 Teams Activity Feed 알림 대상에서 제외됩니다."));
        }

        var renderResult = TeamsActivityNotificationRenderer.Render(message, teamsActivity);
        if (!TeamsActivityNotificationRenderer.IsDeclaredActivityType(renderResult.ActivityType, teamsActivity.ActivityTypes))
        {
            return Task.FromResult(NotificationChannelResult.Failed(
                "TeamsActivityInvalidActivityType",
                "Teams 앱 manifest에 선언되지 않은 activityType입니다."));
        }

        if (teamsActivity.DryRun)
        {
            return Task.FromResult(NotificationChannelResult.DryRunSent());
        }

        if (teamsActivity.RequireEntraUser
            && !string.Equals(message.RecipientAuthProvider, "EntraId", StringComparison.Ordinal))
        {
            return Task.FromResult(NotificationChannelResult.Suppressed(
                "TeamsActivityUserNotEntra",
                "EntraId 사용자가 아니어서 Teams Activity Feed actual 발송을 생략했습니다."));
        }

        var graphUserId = !string.IsNullOrWhiteSpace(message.RecipientEntraObjectId)
            ? message.RecipientEntraObjectId.Trim()
            : teamsActivity.UseUserPrincipalNameFallback && !string.IsNullOrWhiteSpace(message.RecipientEmail)
                ? message.RecipientEmail.Trim()
                : null;
        if (string.IsNullOrWhiteSpace(graphUserId))
        {
            return Task.FromResult(NotificationChannelResult.Suppressed(
                "TeamsActivityMissingUserId",
                "Teams Activity Feed 대상 사용자의 Entra object id가 없습니다."));
        }

        return teamsActivityClient.SendAsync(
            new TeamsActivitySendRequest(
                graphUserId,
                renderResult.ActivityType,
                renderResult.TopicValue,
                renderResult.TopicWebUrl,
                renderResult.PreviewText,
                renderResult.TemplateParameters,
                teamsActivity.TeamsAppId,
                message.CorrelationId),
            cancellationToken);
    }
}

public sealed class MailChannelHandler(
    IOptionsMonitor<NotificationOptions> options,
    IMailClient mailClient)
    : INotificationChannelHandler
{
    public string Channel => NotificationDeliveryChannels.Mail;

    public Task<NotificationChannelResult> SendAsync(NotificationDeliveryMessage message, CancellationToken cancellationToken)
    {
        var mail = options.CurrentValue.Mail;
        if (!mail.Enabled)
        {
            return Task.FromResult(NotificationChannelResult.Disabled("MailDisabled", "메일 발송이 비활성화되어 있습니다."));
        }

        if (string.IsNullOrWhiteSpace(message.RecipientEmail))
        {
            return Task.FromResult(NotificationChannelResult.Suppressed("RecipientEmailMissing", "사용자 이메일이 없어 메일을 보내지 않았습니다."));
        }

        if (mail.DryRun
            || string.IsNullOrWhiteSpace(mail.Provider)
            || string.Equals(mail.Provider, "DryRun", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(NotificationChannelResult.DryRunSent());
        }

        return mailClient.SendAsync(
            new MailDeliveryPayload(
                message.RecipientEmail,
                message.Subject,
                message.Body,
                message.SenderUserId ?? mail.SenderUserId,
                message.SenderAddress ?? mail.SenderAddress,
                message.SaveToSentItems,
                message.CorrelationId),
            cancellationToken);
    }
}
