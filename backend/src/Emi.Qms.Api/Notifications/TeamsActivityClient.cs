using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public interface ITeamsActivityClient
{
    Task<NotificationChannelResult> SendAsync(TeamsActivitySendRequest request, CancellationToken cancellationToken);
}

public sealed record TeamsActivitySendRequest(
    string UserId,
    string ActivityType,
    string TopicSource,
    string TopicValue,
    string? TopicWebUrl,
    string PreviewText,
    IReadOnlyDictionary<string, string> TemplateParameters,
    string? TeamsAppId,
    string? CorrelationId);

public static class TeamsActivityNotificationRenderer
{
    private const int PreviewTextMaxLength = 150;

    public static TeamsActivityRenderResult Render(
        NotificationDeliveryMessage message,
        NotificationTeamsActivityOptions options)
    {
        var activityType = ResolveActivityType(message, options.ActivityTypes);
        var title = Trim(ResolveTitle(message), 120);
        var topicSource = ResolveTopicSource(message);
        var topicValue = ResolveTopicValue(message, topicSource);
        var topicWebUrl = string.Equals(topicSource, "entityUrl", StringComparison.Ordinal)
            ? null
            : ResolveTopicWebUrl(message.LinkUrl, options.TopicWebUrl);
        var previewText = Trim(ResolvePreviewText(message, activityType, title, options.ActivityTypes), PreviewTextMaxLength);
        var templateParameters = BuildTemplateParameters(activityType, message, title, options.ActivityTypes);

        return new TeamsActivityRenderResult(
            activityType,
            topicSource,
            topicValue,
            topicWebUrl,
            previewText,
            templateParameters);
    }

    public static bool IsDeclaredActivityType(string activityType, NotificationTeamsActivityTypeOptions options)
    {
        return new[]
            {
                options.WorkItemAssigned,
                options.DeadlineApproaching,
                options.DeadlineOverdue,
                options.UrgentPending,
                options.DailyDigest,
                options.ProjectCompleted
            }
            .Any(value => string.Equals(value, activityType, StringComparison.Ordinal));
    }

    private static string ResolveActivityType(
        NotificationDeliveryMessage message,
        NotificationTeamsActivityTypeOptions options)
    {
        if (!string.IsNullOrWhiteSpace(message.TeamsActivityType))
        {
            return message.TeamsActivityType.Trim();
        }

        return message.DeliveryType switch
        {
            NotificationDeliveryTypes.WorkItemCreated => options.WorkItemAssigned,
            NotificationDeliveryTypes.DueSoonL0 => options.DeadlineApproaching,
            NotificationDeliveryTypes.OverdueL1
                or NotificationDeliveryTypes.OverdueL2
                or NotificationDeliveryTypes.OverdueL3 => options.DeadlineOverdue,
            NotificationDeliveryTypes.UrgentBlocking => options.UrgentPending,
            NotificationDeliveryTypes.DailyDigest => options.DailyDigest,
            NotificationDeliveryTypes.ProjectCompletion => options.ProjectCompleted,
            _ => options.WorkItemAssigned
        };
    }

    private static IReadOnlyDictionary<string, string> BuildTemplateParameters(
        string activityType,
        NotificationDeliveryMessage message,
        string title,
        NotificationTeamsActivityTypeOptions options)
    {
        if (string.Equals(activityType, options.DeadlineOverdue, StringComparison.Ordinal))
        {
            return new Dictionary<string, string>
            {
                ["escalationLevel"] = ResolveEscalationLevel(message.DeliveryType),
                ["taskName"] = title
            };
        }

        if (string.Equals(activityType, options.UrgentPending, StringComparison.Ordinal))
        {
            return new Dictionary<string, string>
            {
                ["pendingType"] = "긴급",
                ["title"] = title
            };
        }

        if (string.Equals(activityType, options.DeadlineApproaching, StringComparison.Ordinal))
        {
            return new Dictionary<string, string>
            {
                ["taskName"] = title,
                ["dueDate"] = "예정일"
            };
        }

        if (string.Equals(activityType, options.DailyDigest, StringComparison.Ordinal))
        {
            return new Dictionary<string, string>
            {
                ["title"] = title
            };
        }

        if (string.Equals(activityType, options.ProjectCompleted, StringComparison.Ordinal))
        {
            return new Dictionary<string, string>
            {
                ["projectName"] = title
            };
        }

        return new Dictionary<string, string>
        {
            ["taskName"] = title
        };
    }

    private static string ResolveEscalationLevel(string deliveryType)
    {
        return deliveryType switch
        {
            NotificationDeliveryTypes.OverdueL1 => "L1",
            NotificationDeliveryTypes.OverdueL2 => "L2",
            NotificationDeliveryTypes.OverdueL3 => "L3",
            _ => "L0"
        };
    }

    private static string ResolveTitle(NotificationDeliveryMessage message)
    {
        return string.IsNullOrWhiteSpace(message.Subject)
            ? "EMI 프로젝트 통합관리시스템 알림"
            : message.Subject.Trim();
    }

    private static string ResolveTopicSource(NotificationDeliveryMessage message)
    {
        return string.Equals(message.TeamsActivityTopicSource, "entityUrl", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(message.TeamsActivityTopicValue)
            ? "entityUrl"
            : "text";
    }

    private static string ResolveTopicValue(NotificationDeliveryMessage message, string topicSource)
    {
        if (string.Equals(topicSource, "entityUrl", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(message.TeamsActivityTopicValue))
        {
            return message.TeamsActivityTopicValue.Trim();
        }

        if (!string.IsNullOrWhiteSpace(message.Subject))
        {
            return Trim(message.Subject.Trim(), 120);
        }

        return message.DeliveryType;
    }

    private static string ResolvePreviewText(
        NotificationDeliveryMessage message,
        string activityType,
        string title,
        NotificationTeamsActivityTypeOptions options)
    {
        if (string.Equals(activityType, options.DeadlineOverdue, StringComparison.Ordinal))
        {
            return $"{title} / 예정일 초과 / 담당자 확인 필요";
        }

        if (string.Equals(activityType, options.DeadlineApproaching, StringComparison.Ordinal))
        {
            return $"{title} / 예정일 임박 / 담당자 확인 필요";
        }

        if (string.Equals(activityType, options.UrgentPending, StringComparison.Ordinal))
        {
            return $"{title} / 조치 담당자 확인 필요";
        }

        if (string.Equals(activityType, options.DailyDigest, StringComparison.Ordinal))
        {
            return "오늘의 업무와 담당 프로젝트 요약";
        }

        if (string.Equals(activityType, options.ProjectCompleted, StringComparison.Ordinal))
        {
            return $"{title} / 프로젝트 완료";
        }

        if (!string.IsNullOrWhiteSpace(message.Body))
        {
            return SingleLine(message.Body);
        }

        return $"{title} / 내 업무 확인 필요";
    }

    private static string ResolveTopicWebUrl(string? linkUrl, string? configuredTopicWebUrl)
    {
        var configuredBaseUrl = ResolveConfiguredTopicBaseUrl(configuredTopicWebUrl);
        if (!string.IsNullOrWhiteSpace(linkUrl))
        {
            var trimmedLink = linkUrl.Trim();
            if (Uri.TryCreate(trimmedLink, UriKind.Absolute, out var absoluteLink)
                && absoluteLink.Scheme == Uri.UriSchemeHttps)
            {
                return trimmedLink;
            }

            if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri)
                && TryCombineWithOrigin(baseUri, trimmedLink, out var combinedLink)
                && combinedLink.Scheme == Uri.UriSchemeHttps)
            {
                return combinedLink.ToString();
            }
        }

        return configuredBaseUrl;
    }

    private static string ResolveConfiguredTopicBaseUrl(string? configuredTopicWebUrl)
    {
        return string.IsNullOrWhiteSpace(configuredTopicWebUrl)
            ? "https://localhost:5174/teams/activity"
            : configuredTopicWebUrl.Trim();
    }

    private static bool TryCombineWithOrigin(Uri baseUri, string relativeLink, out Uri combinedLink)
    {
        combinedLink = null!;
        if (string.IsNullOrWhiteSpace(relativeLink))
        {
            return false;
        }

        var origin = new Uri(baseUri.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
        try
        {
            combinedLink = new Uri(origin, relativeLink.TrimStart('/'));
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string Trim(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string SingleLine(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    internal static bool IsTeamsDeepLink(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "teams.microsoft.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/l/", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record TeamsActivityRenderResult(
    string ActivityType,
    string TopicSource,
    string TopicValue,
    string? TopicWebUrl,
    string PreviewText,
    IReadOnlyDictionary<string, string> TemplateParameters);

public sealed class GraphTeamsActivityClient(
    HttpClient httpClient,
    IOptionsMonitor<NotificationOptions> options,
    TimeProvider timeProvider)
    : ITeamsActivityClient
{
    private string? cachedAccessToken;
    private DateTimeOffset expiresAtUtc;

    public async Task<NotificationChannelResult> SendAsync(TeamsActivitySendRequest request, CancellationToken cancellationToken)
    {
        var teamsActivity = options.CurrentValue.TeamsActivity;
        var validationError = Validate(teamsActivity, request);
        if (validationError is not null)
        {
            return validationError;
        }

        var token = await GetTokenAsync(teamsActivity, cancellationToken);
        if (!token.Succeeded || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return NotificationChannelResult.Failed(
                token.ErrorCode ?? "TeamsActivityTokenFailed",
                token.ErrorMessage ?? "Teams Activity Graph token 요청이 실패했습니다.");
        }

        var clientRequestId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId;

        using var response = await SendActivityNotificationAsync(teamsActivity, request, token.AccessToken, clientRequestId, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return NotificationChannelResult.Sent(BuildProviderMessageId(response, clientRequestId));
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var graphError = ParseGraphError(responseBody);
        if (IsInvalidInstalledAppId(graphError)
            && !string.IsNullOrWhiteSpace(teamsActivity.TeamsAppId)
            && string.Equals(request.TopicSource, "entityUrl", StringComparison.Ordinal))
        {
            var resolvedTopicValue = await ResolveInstalledAppEntityUrlAsync(teamsActivity, request.UserId, token.AccessToken, cancellationToken);
            if (!string.IsNullOrWhiteSpace(resolvedTopicValue)
                && !string.Equals(resolvedTopicValue, request.TopicValue, StringComparison.Ordinal))
            {
                var retryRequest = request with
                {
                    TopicSource = "entityUrl",
                    TopicValue = resolvedTopicValue,
                    TopicWebUrl = null
                };
                using var retryResponse = await SendActivityNotificationAsync(teamsActivity, retryRequest, token.AccessToken, clientRequestId, cancellationToken);
                if (retryResponse.StatusCode == HttpStatusCode.NoContent)
                {
                    return NotificationChannelResult.Sent(BuildProviderMessageId(retryResponse, clientRequestId));
                }

                responseBody = await retryResponse.Content.ReadAsStringAsync(cancellationToken);
                graphError = ParseGraphError(responseBody);
                var retryErrorCode = ClassifyGraphError(retryResponse.StatusCode, graphError);
                return new NotificationChannelResult(
                    NotificationDeliveryStatuses.Failed,
                    BuildProviderMessageId(retryResponse, clientRequestId),
                    retryErrorCode,
                    BuildGraphErrorMessage(retryResponse.StatusCode, retryErrorCode, graphError));
            }
        }

        var errorCode = ClassifyGraphError(response.StatusCode, graphError);
        return new NotificationChannelResult(
            NotificationDeliveryStatuses.Failed,
            BuildProviderMessageId(response, clientRequestId),
            errorCode,
            BuildGraphErrorMessage(response.StatusCode, errorCode, graphError));
    }

    private async Task<HttpResponseMessage> SendActivityNotificationAsync(
        NotificationTeamsActivityOptions teamsActivity,
        TeamsActivitySendRequest request,
        string accessToken,
        string clientRequestId,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{teamsActivity.BaseUrl.TrimEnd('/')}/users/{Uri.EscapeDataString(request.UserId)}/teamwork/sendActivityNotification";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(GraphTeamsActivityNotificationRequest.FromRequest(request))
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Add("client-request-id", clientRequestId);
        httpRequest.Headers.Add("return-client-request-id", "true");
        return await httpClient.SendAsync(httpRequest, cancellationToken);
    }

    private async Task<GraphAccessTokenResult> GetTokenAsync(
        NotificationTeamsActivityOptions teamsActivity,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(cachedAccessToken) && expiresAtUtc > now.AddMinutes(5))
        {
            return new GraphAccessTokenResult(true, cachedAccessToken, null, null);
        }

        var tokenEndpoint = $"{teamsActivity.AuthorityHost.TrimEnd('/')}/{Uri.EscapeDataString(teamsActivity.TenantId!)}/oauth2/v2.0/token";
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = teamsActivity.ClientId!,
                ["client_secret"] = teamsActivity.ClientSecret!,
                ["scope"] = teamsActivity.Scope,
                ["grant_type"] = "client_credentials"
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var tokenResponse = await response.Content.ReadFromJsonAsync<GraphTokenResponse>(cancellationToken);
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
        {
            return new GraphAccessTokenResult(
                false,
                null,
                "TeamsActivityTokenFailed",
                $"Teams Activity Graph token 요청이 실패했습니다. HTTP {(int)response.StatusCode}");
        }

        cachedAccessToken = tokenResponse.AccessToken;
        expiresAtUtc = now.AddSeconds(Math.Max(60, tokenResponse.ExpiresIn));
        return new GraphAccessTokenResult(true, cachedAccessToken, null, null);
    }

    private async Task<string?> ResolveInstalledAppEntityUrlAsync(
        NotificationTeamsActivityOptions teamsActivity,
        string userId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(teamsActivity.TeamsAppId))
        {
            return null;
        }

        var endpoint = $"{teamsActivity.BaseUrl.TrimEnd('/')}/users/{Uri.EscapeDataString(userId)}/teamwork/installedApps?$expand=teamsApp,teamsAppDefinition";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var installedApps = await response.Content.ReadFromJsonAsync<GraphInstalledAppsResponse>(cancellationToken);
        var matchingApp = installedApps?.Value.FirstOrDefault(app =>
            string.Equals(app.TeamsApp?.ExternalId, teamsActivity.TeamsAppId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(app.TeamsApp?.Id, teamsActivity.TeamsAppId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(app.TeamsAppDefinition?.TeamsAppId, teamsActivity.TeamsAppId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(app.TeamsAppDefinition?.Id, teamsActivity.TeamsAppId, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(matchingApp?.Id))
        {
            return null;
        }

        return teamsActivity.BaseUrl.TrimEnd('/')
            + "/users/"
            + Uri.EscapeDataString(userId)
            + "/teamwork/installedApps/"
            + Uri.EscapeDataString(matchingApp.Id);
    }

    private static NotificationChannelResult? Validate(NotificationTeamsActivityOptions options, TeamsActivitySendRequest request)
    {
        if (string.IsNullOrWhiteSpace(options.TenantId)
            || string.IsNullOrWhiteSpace(options.ClientId)
            || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            return NotificationChannelResult.Failed(
                "TeamsActivityGraphConfigMissing",
                "Teams Activity Graph client credentials 설정이 누락되었습니다.");
        }

        if (string.Equals(request.TopicSource, "text", StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(request.TopicWebUrl)
                || !Uri.TryCreate(request.TopicWebUrl, UriKind.Absolute, out var topicUri)
                || topicUri.Scheme != Uri.UriSchemeHttps))
        {
            return NotificationChannelResult.Failed(
                "TeamsActivityInvalidTopic",
                "Teams Activity topic webUrl은 https URL이어야 합니다.");
        }

        return null;
    }

    private static string ClassifyGraphError(HttpStatusCode statusCode, GraphError? graphError)
    {
        var message = graphError?.Message ?? "";
        if (IsAppNotInstalled(statusCode, graphError))
        {
            return "TeamsActivityAppNotInstalled";
        }

        if (message.Contains("Invalid 'webUrl'", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Invalid webUrl", StringComparison.OrdinalIgnoreCase)
            || message.Contains("validDomains", StringComparison.OrdinalIgnoreCase)
            || message.Contains("valid domains", StringComparison.OrdinalIgnoreCase))
        {
            return "TeamsActivityInvalidTopic";
        }

        if (IsInvalidInstalledAppId(graphError))
        {
            return "TeamsActivityInvalidInstalledAppId";
        }

        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "TeamsActivityPermissionDenied",
            HttpStatusCode.NotFound => "TeamsActivityUserOrAppNotFound",
            HttpStatusCode.BadRequest => "TeamsActivityInvalidRequest",
            HttpStatusCode.TooManyRequests => "TeamsActivityThrottled",
            _ when (int)statusCode >= 500 => "TeamsActivityGraphServerError",
            _ => "TeamsActivityGraphError"
        };
    }

    private static bool IsInvalidInstalledAppId(GraphError? graphError)
    {
        var message = graphError?.Message ?? "";
        return message.Contains("app installation id", StringComparison.OrdinalIgnoreCase)
            && message.Contains("not a valid app installation id", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAppNotInstalled(HttpStatusCode statusCode, GraphError? graphError)
    {
        var message = graphError?.Message ?? "";
        return (message.Contains("Failed to find Teams application", StringComparison.OrdinalIgnoreCase)
                && message.Contains("installed applications", StringComparison.OrdinalIgnoreCase))
            || (statusCode == HttpStatusCode.NotFound
                && message.Contains("install", StringComparison.OrdinalIgnoreCase)
                && message.Contains("app", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildGraphErrorMessage(HttpStatusCode statusCode, string errorCode, GraphError? graphError)
    {
        var baseMessage = errorCode switch
        {
            "TeamsActivityAppNotInstalled" => "수신자의 Teams 앱 설치 상태 또는 Teams 앱 정책을 확인하세요.",
            "TeamsActivityInvalidTopic" => "Teams manifest validDomains와 webUrl 도메인을 확인하세요.",
            "TeamsActivityInvalidInstalledAppId" => "Teams Activity InstalledAppId가 Graph 설치 앱 ID와 일치하지 않습니다. 수신자 기준 installedApps 최상위 id를 확인해야 합니다.",
            "TeamsActivityPermissionDenied" => "Teams Activity Graph 권한 또는 관리자 동의가 부족합니다.",
            "TeamsActivityThrottled" => "Teams Activity Graph 요청이 제한되었습니다. 잠시 후 재시도해야 합니다.",
            _ => $"Teams Activity Graph 요청이 실패했습니다. HTTP {(int)statusCode}"
        };
        var graphDetail = CompactGraphDetail($"{graphError?.Code} {graphError?.Message}".Trim());
        return string.IsNullOrWhiteSpace(graphDetail)
            ? baseMessage
            : $"{baseMessage} Graph: {TruncateGraphDetail(graphDetail, 300)}";
    }

    private static string CompactGraphDetail(string value)
    {
        var singleLine = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return Regex.Replace(singleLine, @"[A-Za-z0-9_-]{16,}", "[MASKED_ID]");
    }

    private static string TruncateGraphDetail(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static GraphError? ParseGraphError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GraphErrorEnvelope>(responseBody)?.Error;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildProviderMessageId(HttpResponseMessage response, string clientRequestId)
    {
        var requestId = response.Headers.TryGetValues("request-id", out var requestIds)
            ? requestIds.FirstOrDefault()
            : null;
        return string.IsNullOrWhiteSpace(requestId)
            ? $"teams-activity-sent;client-request-id={clientRequestId}"
            : $"teams-activity-sent;request-id={requestId};client-request-id={clientRequestId}";
    }
}

public sealed record GraphTeamsActivityNotificationRequest(
    [property: JsonPropertyName("topic")] GraphTeamsActivityTopic Topic,
    [property: JsonPropertyName("activityType")] string ActivityType,
    [property: JsonPropertyName("previewText")] GraphTeamsActivityPreviewText PreviewText,
    [property: JsonPropertyName("templateParameters")] IReadOnlyList<GraphTeamsActivityTemplateParameter> TemplateParameters,
    [property: JsonPropertyName("teamsAppId")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TeamsAppId)
{
    public static GraphTeamsActivityNotificationRequest FromRequest(TeamsActivitySendRequest request)
    {
        var teamsAppId = string.Equals(request.TopicSource, "entityUrl", StringComparison.Ordinal)
            ? null
            : request.TeamsAppId;

        return new GraphTeamsActivityNotificationRequest(
            new GraphTeamsActivityTopic(request.TopicSource, request.TopicValue, request.TopicWebUrl),
            request.ActivityType,
            new GraphTeamsActivityPreviewText(request.PreviewText),
            request.TemplateParameters
                .Select(parameter => new GraphTeamsActivityTemplateParameter(parameter.Key, parameter.Value))
                .ToArray(),
            string.IsNullOrWhiteSpace(teamsAppId) ? null : teamsAppId);
    }
}

public sealed record GraphTeamsActivityTopic(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("webUrl")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? WebUrl);

public sealed record GraphTeamsActivityPreviewText(
    [property: JsonPropertyName("content")] string Content);

public sealed record GraphTeamsActivityTemplateParameter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value);

public sealed record GraphInstalledAppsResponse(
    [property: JsonPropertyName("value")] IReadOnlyList<GraphInstalledApp> Value);

public sealed record GraphInstalledApp(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("teamsApp")] GraphTeamsApp? TeamsApp,
    [property: JsonPropertyName("teamsAppDefinition")] GraphTeamsAppDefinition? TeamsAppDefinition);

public sealed record GraphTeamsApp(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("externalId")] string? ExternalId,
    [property: JsonPropertyName("displayName")] string? DisplayName);

public sealed record GraphTeamsAppDefinition(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("teamsAppId")] string? TeamsAppId,
    [property: JsonPropertyName("displayName")] string? DisplayName);

public sealed record GraphErrorEnvelope(
    [property: JsonPropertyName("error")] GraphError? Error);

public sealed record GraphError(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message);
