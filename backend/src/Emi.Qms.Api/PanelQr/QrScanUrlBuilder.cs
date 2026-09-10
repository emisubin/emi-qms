namespace Emi.Qms.Api.PanelQr;

public sealed class QrScanUrlBuilder(IConfiguration configuration, IHostEnvironment environment)
{
    private const string DevelopmentFallbackOrigin = "https://localhost:5174";

    public string BuildForPath(string path)
    {
        var configuredOrigins = configuration["Qr:ScanOrigin"]
            ?? configuration["QR_SCAN_ORIGIN"]
            ?? configuration["Frontend:Origin"]
            ?? configuration["FRONTEND_ORIGIN"];
        var origin = configuredOrigins?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (origin is null && (environment.IsDevelopment() || environment.IsEnvironment("Testing")))
        {
            origin = DevelopmentFallbackOrigin;
        }
        if (origin is null)
        {
            throw new InvalidOperationException(
                "QR scan origin is not configured. Configure Qr:ScanOrigin for this environment.");
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            || (originUri.Scheme != Uri.UriSchemeHttp && originUri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(originUri.Host)
            || !string.IsNullOrEmpty(originUri.UserInfo)
            || originUri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(originUri.Query)
            || !string.IsNullOrEmpty(originUri.Fragment)
            || (environment.IsProduction() && originUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "QR scan origin must be an origin-only HTTP(S) URL, and production requires HTTPS.");
        }

        return $"{originUri.GetLeftPart(UriPartial.Authority)}/{path.TrimStart('/')}";
    }
}
