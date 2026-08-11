namespace Emi.Qms.Api.Notifications;

public static class WebPushKeyValidation
{
    public static bool IsValidSubscriptionKeys(string p256dh, string auth)
    {
        return TryDecodeBase64Url(p256dh, out var publicKey)
            && publicKey.Length == 65
            && publicKey[0] == 4
            && TryDecodeBase64Url(auth, out var authSecret)
            && authSecret.Length == 16;
    }

    public static void EnsureValidSubscriptionKeys(string p256dh, string auth)
    {
        if (!IsValidSubscriptionKeys(p256dh, auth))
        {
            throw new ArgumentException("브라우저에서 발급된 올바른 Web Push 암호화 키가 필요합니다.");
        }
    }

    private static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            return false;
        }

        try
        {
            var padding = new string('=', (4 - value.Length % 4) % 4);
            bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + padding);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
