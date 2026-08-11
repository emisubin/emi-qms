namespace Emi.Qms.Api.Notifications;

public static class WebPushEndpointPolicy
{
    public static bool HasValidAllowedHosts(NotificationWebPushOptions options)
    {
        return options.AllowedEndpointHostSuffixes.Length > 0
            && options.AllowedEndpointHostSuffixes.All(IsValidHostSuffix);
    }

    public static bool IsAllowed(string endpoint, NotificationWebPushOptions options)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        return options.AllowedEndpointHostSuffixes
            .Select(NormalizeHostSuffix)
            .Where(suffix => suffix is not null)
            .Any(suffix => host == suffix || host.EndsWith($".{suffix}", StringComparison.Ordinal));
    }

    public static void EnsureAllowed(string endpoint, NotificationWebPushOptions options)
    {
        if (!IsAllowed(endpoint, options))
        {
            throw new ArgumentException("승인된 Web Push 서비스 주소만 등록할 수 있습니다.");
        }
    }

    private static bool IsValidHostSuffix(string? value)
    {
        var normalized = NormalizeHostSuffix(value);
        return normalized is not null
            && Uri.CheckHostName(normalized) == UriHostNameType.Dns;
    }

    private static string? NormalizeHostSuffix(string? value)
    {
        var normalized = value?.Trim().TrimStart('.').TrimEnd('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
