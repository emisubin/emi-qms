using Emi.Qms.Api.BusinessUnits;
using Microsoft.AspNetCore.WebUtilities;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationLinkBuilder(IConfiguration configuration)
{
    public string? BuildNotificationDetailUrl(Guid notificationId, string? businessUnitCode = null)
    {
        return BuildBusinessUrl($"/teams/activity/notifications/{notificationId:D}", businessUnitCode);
    }

    public string? BuildTeamsActivityNotificationWebUrl(Guid notificationId, string? businessUnitCode = null)
    {
        var notificationUrl = BuildNotificationDetailUrl(notificationId, businessUnitCode);
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

        var notificationContext = businessUnitCode is BusinessUnitCodes.Cheongju or BusinessUnitCodes.Osan
            ? $"notification:{businessUnitCode}:{notificationId:D}"
            : $"notification:{notificationId:D}";
        var context = $$"""{"subEntityId":"{{notificationContext}}"}""";
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

    public string? BuildBusinessUrl(string path, string? businessUnitCode = null)
    {
        if (!path.StartsWith('/') || path.StartsWith("//"))
        {
            return null;
        }

        var businessPath = AddBusinessUnitHint(path, businessUnitCode);
        return businessPath is null ? null : BuildUrl(businessPath);
    }

    private static string? AddBusinessUnitHint(string path, string? businessUnitCode)
    {
        if (businessUnitCode is not (BusinessUnitCodes.Cheongju or BusinessUnitCodes.Osan))
        {
            return path;
        }

        var queryStart = path.IndexOf('?');
        if (queryStart >= 0)
        {
            var fragmentStart = path.IndexOf('#', queryStart);
            var query = fragmentStart >= 0
                ? path[(queryStart + 1)..fragmentStart]
                : path[(queryStart + 1)..];
            var parsed = QueryHelpers.ParseQuery(query);
            if (parsed.TryGetValue("businessUnit", out var existing))
            {
                return existing.Count == 1
                    && string.Equals(existing[0], businessUnitCode, StringComparison.Ordinal)
                        ? path
                        : null;
            }
        }

        return QueryHelpers.AddQueryString(path, "businessUnit", businessUnitCode);
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
