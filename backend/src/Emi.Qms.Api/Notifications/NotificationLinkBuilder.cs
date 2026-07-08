namespace Emi.Qms.Api.Notifications;

public sealed class NotificationLinkBuilder(IConfiguration configuration)
{
    public string? BuildNotificationDetailUrl(Guid notificationId)
    {
        return BuildUrl($"/teams/activity/notifications/{notificationId:D}");
    }

    public string? BuildTeamsActivityNotificationWebUrl(Guid notificationId)
    {
        var notificationUrl = BuildNotificationDetailUrl(notificationId);
        if (string.IsNullOrWhiteSpace(notificationUrl)
            || !Uri.TryCreate(notificationUrl, UriKind.Absolute, out var notificationUri)
            || notificationUri.Scheme != Uri.UriSchemeHttps)
        {
            return notificationUrl;
        }

        var teamsDeepLinkAppId = configuration["Notifications:TeamsActivity:TeamsCatalogAppId"];
        if (string.IsNullOrWhiteSpace(teamsDeepLinkAppId))
        {
            teamsDeepLinkAppId = configuration["Notifications:TeamsActivity:TeamsManifestExternalId"];
        }

        if (string.IsNullOrWhiteSpace(teamsDeepLinkAppId))
        {
            teamsDeepLinkAppId = configuration["Notifications:TeamsActivity:ManifestId"];
        }

        if (string.IsNullOrWhiteSpace(teamsDeepLinkAppId))
        {
            teamsDeepLinkAppId = configuration["Notifications:TeamsActivity:TeamsAppId"];
        }

        if (string.IsNullOrWhiteSpace(teamsDeepLinkAppId))
        {
            return notificationUrl;
        }

        var entityId = configuration["Notifications:TeamsActivity:TeamsStaticTabEntityId"];
        if (string.IsNullOrWhiteSpace(entityId))
        {
            entityId = configuration["Notifications:TeamsActivity:DeepLinkEntityId"];
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            entityId = "home";
        }

        var context = $$"""{"subEntityId":"notification:{{notificationId:D}}"}""";
        return "https://teams.microsoft.com/l/entity/"
            + Uri.EscapeDataString(teamsDeepLinkAppId.Trim())
            + "/"
            + Uri.EscapeDataString(entityId.Trim())
            + "?webUrl="
            + Uri.EscapeDataString(notificationUri.ToString())
            + "&label="
            + Uri.EscapeDataString("알림상세")
            + "&context="
            + Uri.EscapeDataString(context);
    }

    public string? BuildDeliveryDetailUrl(Guid deliveryId)
    {
        return BuildUrl($"/teams/activity/deliveries/{deliveryId:D}");
    }

    private string? BuildUrl(string path)
    {
        var baseUrl = ResolveBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return path;
        }

        return $"{baseUrl.TrimEnd('/')}{path}";
    }

    private string? ResolveBaseUrl()
    {
        var configuredUrl = configuration["Notifications:Links:BaseUrl"];
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            configuredUrl = configuration["Notifications:TeamsActivity:TopicWebUrl"];
        }

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            configuredUrl = configuration["FRONTEND_ORIGIN"]
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(origin => origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(configuredUrl)
            || !Uri.TryCreate(configuredUrl.Trim(), UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var baseUrl = baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        if (baseUrl.EndsWith("/teams/activity", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = baseUrl[..^"/teams/activity".Length];
        }

        return baseUrl;
    }
}
