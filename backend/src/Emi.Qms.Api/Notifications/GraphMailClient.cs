using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public interface IGraphTokenProvider
{
    Task<GraphAccessTokenResult> GetTokenAsync(CancellationToken cancellationToken);
}

public sealed record GraphAccessTokenResult(
    bool Succeeded,
    string? AccessToken,
    string? ErrorCode,
    string? ErrorMessage);

public sealed class GraphClientCredentialsTokenProvider(
    HttpClient httpClient,
    IOptionsMonitor<NotificationOptions> options,
    TimeProvider timeProvider)
    : IGraphTokenProvider
{
    private string? cachedAccessToken;
    private DateTimeOffset expiresAtUtc;

    public async Task<GraphAccessTokenResult> GetTokenAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(cachedAccessToken) && expiresAtUtc > now.AddMinutes(5))
        {
            return new GraphAccessTokenResult(true, cachedAccessToken, null, null);
        }

        var graph = options.CurrentValue.Graph;
        var validationError = ValidateGraphOptions(graph);
        if (validationError is not null)
        {
            return validationError;
        }

        var tokenEndpoint = BuildTokenEndpoint(graph);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = graph.ClientId!,
                ["client_secret"] = graph.ClientSecret!,
                ["scope"] = graph.Scope,
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
                "GraphTokenFailed",
                $"Graph token 요청이 실패했습니다. HTTP {(int)response.StatusCode}");
        }

        cachedAccessToken = tokenResponse.AccessToken;
        expiresAtUtc = now.AddSeconds(Math.Max(60, tokenResponse.ExpiresIn));
        return new GraphAccessTokenResult(true, cachedAccessToken, null, null);
    }

    private static GraphAccessTokenResult? ValidateGraphOptions(NotificationGraphOptions graph)
    {
        if (string.IsNullOrWhiteSpace(graph.TenantId)
            || string.IsNullOrWhiteSpace(graph.ClientId)
            || string.IsNullOrWhiteSpace(graph.ClientSecret))
        {
            return new GraphAccessTokenResult(
                false,
                null,
                "GraphConfigMissing",
                "Graph client credentials 설정이 누락되었습니다.");
        }

        return null;
    }

    private static string BuildTokenEndpoint(NotificationGraphOptions graph)
    {
        var authorityHost = graph.AuthorityHost.TrimEnd('/');
        return $"{authorityHost}/{Uri.EscapeDataString(graph.TenantId!)}/oauth2/v2.0/token";
    }
}

public sealed class GraphMailClient(
    HttpClient httpClient,
    IGraphTokenProvider tokenProvider,
    IOptionsMonitor<NotificationOptions> options)
    : IGraphMailClient
{
    public async Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken)
    {
        var mail = options.CurrentValue.Mail;
        if (!string.Equals(mail.Provider, "Graph", StringComparison.OrdinalIgnoreCase))
        {
            return NotificationChannelResult.Disabled(
                "MailProviderUnsupported",
                "지원하지 않는 메일 provider입니다.");
        }

        var sender = !string.IsNullOrWhiteSpace(payload.SenderUserId)
            ? payload.SenderUserId
            : !string.IsNullOrWhiteSpace(payload.SenderAddress)
                ? payload.SenderAddress
                : !string.IsNullOrWhiteSpace(mail.SenderUserId)
                    ? mail.SenderUserId
                    : mail.SenderAddress;
        if (string.IsNullOrWhiteSpace(sender))
        {
            return NotificationChannelResult.Disabled(
                "GraphMailSenderMissing",
                "Graph 메일 발신 계정 설정이 누락되었습니다.");
        }

        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        if (!token.Succeeded || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return NotificationChannelResult.Failed(
                token.ErrorCode ?? "GraphTokenFailed",
                token.ErrorMessage ?? "Graph token 요청이 실패했습니다.");
        }

        var graph = options.CurrentValue.Graph;
        var endpoint = $"{graph.BaseUrl.TrimEnd('/')}/users/{Uri.EscapeDataString(sender)}/sendMail";
        var clientRequestId = string.IsNullOrWhiteSpace(payload.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : payload.CorrelationId;
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(GraphSendMailRequest.FromPayload(payload))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Headers.Add("client-request-id", clientRequestId);
        request.Headers.Add("return-client-request-id", "true");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return NotificationChannelResult.Sent(BuildProviderMessageId(response, clientRequestId));
        }

        var errorCode = ClassifyGraphError(response.StatusCode);
        return NotificationChannelResult.Failed(
            errorCode,
            $"Graph sendMail 요청이 실패했습니다. HTTP {(int)response.StatusCode}");
    }

    private static string ClassifyGraphError(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "GraphMailUnauthorized",
            HttpStatusCode.Forbidden => "GraphMailForbidden",
            HttpStatusCode.NotFound => "GraphMailSenderNotFound",
            HttpStatusCode.TooManyRequests => "GraphMailThrottled",
            _ when (int)statusCode >= 500 => "GraphMailServerError",
            _ => "GraphMailRequestFailed"
        };
    }

    private static string BuildProviderMessageId(HttpResponseMessage response, string clientRequestId)
    {
        var requestId = response.Headers.TryGetValues("request-id", out var requestIds)
            ? requestIds.FirstOrDefault()
            : null;
        return string.IsNullOrWhiteSpace(requestId)
            ? $"graph-sendmail-accepted;client-request-id={clientRequestId}"
            : $"graph-sendmail-accepted;request-id={requestId};client-request-id={clientRequestId}";
    }
}

public sealed record GraphSendMailRequest(
    [property: JsonPropertyName("message")] GraphMailMessage Message,
    [property: JsonPropertyName("saveToSentItems")] bool SaveToSentItems)
{
    public static GraphSendMailRequest FromPayload(MailDeliveryPayload payload)
    {
        return new GraphSendMailRequest(
            new GraphMailMessage(
                payload.Subject,
                new GraphMailBody(
                    "Text",
                    payload.Body),
                new[]
                {
                    new GraphMailRecipient(new GraphEmailAddress(payload.RecipientEmail))
                },
                BuildInternetMessageHeaders(payload.CorrelationId)),
            payload.SaveToSentItems);
    }

    private static IReadOnlyList<GraphInternetMessageHeader>? BuildInternetMessageHeaders(string? correlationId)
    {
        return string.IsNullOrWhiteSpace(correlationId)
            ? null
            : new[]
            {
                new GraphInternetMessageHeader("x-emi-notification-test-id", correlationId)
            };
    }
}

public sealed record GraphMailMessage(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("body")] GraphMailBody Body,
    [property: JsonPropertyName("toRecipients")] IReadOnlyList<GraphMailRecipient> ToRecipients,
    [property: JsonPropertyName("internetMessageHeaders")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<GraphInternetMessageHeader>? InternetMessageHeaders);

public sealed record GraphMailBody(
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("content")] string Content);

public sealed record GraphMailRecipient(
    [property: JsonPropertyName("emailAddress")] GraphEmailAddress EmailAddress);

public sealed record GraphEmailAddress(
    [property: JsonPropertyName("address")] string Address);

public sealed record GraphInternetMessageHeader(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value);

internal sealed record GraphTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
