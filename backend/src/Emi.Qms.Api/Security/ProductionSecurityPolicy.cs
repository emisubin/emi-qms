using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Emi.Qms.Api.Security;

public static class ProductionSecurityPolicy
{
    private static readonly TimeSpan MaximumRestoreVerificationAge = TimeSpan.FromDays(90);

    public static void ThrowIfInvalid(IHostEnvironment environment, IConfiguration configuration)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var errors = Evaluate(configuration, DateTimeOffset.UtcNow);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Production security configuration is incomplete: {string.Join(" ", errors)}");
        }
    }

    public static IReadOnlyList<string> Evaluate(
        IConfiguration configuration,
        DateTimeOffset now)
    {
        var errors = new List<string>();

        ValidateHosts(configuration, errors);
        ValidateFrontend(configuration, errors);
        ValidateTrustedProxy(configuration, errors);
        ValidateEntra(configuration, errors);
        ValidateRequestDefenses(configuration, errors);
        ValidateDatabase(configuration, errors);
        ValidateOperations(configuration, now, errors);

        return errors;
    }

    private static void ValidateHosts(IConfiguration configuration, ICollection<string> errors)
    {
        var hosts = Split(configuration["AllowedHosts"]);
        if (hosts.Count == 0
            || hosts.Any(host =>
                host is "*" or "+"
                || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || IPAddress.TryParse(host, out _)
                || IsReservedExampleHost(host)))
        {
            errors.Add("AllowedHosts must contain only exact production DNS names.");
        }
    }

    private static void ValidateFrontend(IConfiguration configuration, ICollection<string> errors)
    {
        var configuredOrigin = configuration["FRONTEND_ORIGIN"]
            ?? configuration["Frontend:Origin"];
        if (!TryGetPublicHttpsUri(configuredOrigin, out var origin)
            || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment))
        {
            errors.Add("Frontend:Origin must be one exact public HTTPS origin.");
            return;
        }

        if (!TryGetPublicHttpsUri(configuration["Frontend:RedirectUri"], out var redirect)
            || !string.Equals(origin.Scheme, redirect.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(origin.Host, redirect.Host, StringComparison.OrdinalIgnoreCase)
            || origin.Port != redirect.Port)
        {
            errors.Add("Frontend:RedirectUri must be HTTPS and use the exact Frontend:Origin.");
        }

        var allowedHosts = Split(configuration["AllowedHosts"]);
        if (!allowedHosts.Contains(origin.Host, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Frontend:Origin host must be present in AllowedHosts.");
        }
    }

    private static void ValidateTrustedProxy(IConfiguration configuration, ICollection<string> errors)
    {
        var proxies = Split(configuration["ReverseProxy:KnownProxies"]);
        if (proxies.Count == 0
            || proxies.Any(proxy => !IPAddress.TryParse(proxy, out var address)
                || address.Equals(IPAddress.Any)
                || address.Equals(IPAddress.IPv6Any)
                || IPAddress.IsLoopback(address)))
        {
            errors.Add("ReverseProxy:KnownProxies must contain exact non-loopback proxy IP addresses.");
        }
    }

    private static void ValidateEntra(IConfiguration configuration, ICollection<string> errors)
    {
        if (!string.Equals(configuration["Authentication:Mode"], "EntraId", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Authentication:Mode must be EntraId.");
        }

        if (!Guid.TryParse(configuration["AzureAd:TenantId"], out var tenantId)
            || tenantId == Guid.Empty
            || !Guid.TryParse(configuration["AzureAd:ClientId"], out var clientId)
            || clientId == Guid.Empty
            || !Guid.TryParse(configuration["AzureAd:SpaClientId"], out var spaClientId)
            || spaClientId == Guid.Empty
            || spaClientId == clientId
            || string.IsNullOrWhiteSpace(configuration["AzureAd:Audience"])
            || string.IsNullOrWhiteSpace(configuration["AzureAd:Domain"])
            || IsReservedExampleHost(configuration["AzureAd:Domain"]!))
        {
            errors.Add("Production Entra tenant, distinct API and SPA clients, audience, and domain are required.");
        }
    }

    private static void ValidateRequestDefenses(IConfiguration configuration, ICollection<string> errors)
    {
        var readLimit = configuration.GetValue<int>("RateLimiting:ReadRequestsPerMinute");
        var mutationLimit = configuration.GetValue<int>("RateLimiting:MutationRequestsPerMinute");
        var uploadLimit = configuration.GetValue<int>("RateLimiting:UploadRequestsPerMinute");
        var healthLimit = configuration.GetValue<int>("RateLimiting:HealthRequestsPerMinute");
        if (!configuration.GetValue("RateLimiting:Enabled", true)
            || readLimit is < 1 or > 6000
            || mutationLimit is < 1 or > 1200
            || uploadLimit is < 1 or > 120
            || healthLimit is < 1 or > 1200)
        {
            errors.Add("Rate limiting must be enabled with bounded request limits.");
        }

        var maximumFileBytes = configuration.GetValue<long>("UploadSecurity:MaximumFileBytes");
        var scannerPort = configuration.GetValue<int>("UploadSecurity:ScannerPort");
        var timeoutSeconds = configuration.GetValue<int>("UploadSecurity:TimeoutSeconds");
        if (!configuration.GetValue<bool>("UploadSecurity:Enabled")
            || !configuration.GetValue<bool>("UploadSecurity:FailClosed")
            || !configuration.GetValue<bool>("UploadSecurity:RejectImageMetadata")
            || string.IsNullOrWhiteSpace(configuration["UploadSecurity:ScannerHost"])
            || scannerPort is < 1 or > 65535
            || timeoutSeconds is < 1 or > 120
            || maximumFileBytes is < 1 or > 33554432)
        {
            errors.Add("Fail-closed upload malware scanning must be enabled.");
        }
    }

    private static void ValidateDatabase(IConfiguration configuration, ICollection<string> errors)
    {
        var connectionString = configuration.GetConnectionString("QmsDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add("The production database connection string is required.");
            return;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (builder.SslMode != SslMode.VerifyFull)
            {
                errors.Add("The production database must use SSL Mode=VerifyFull with certificate validation.");
            }
        }
        catch (ArgumentException)
        {
            errors.Add("The production database connection string is invalid.");
        }
    }

    private static void ValidateOperations(
        IConfiguration configuration,
        DateTimeOffset now,
        ICollection<string> errors)
    {
        var administratorConfiguration =
            configuration["AUTHENTICATION_BOOTSTRAP_ADMIN_EMAILS"]
            ?? configuration["Authentication:BootstrapAdminEmails"];
        var administrators = Split(administratorConfiguration)
            .Where(value => value.Contains('@', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (administrators.Count < 2)
        {
            errors.Add("At least two distinct break-glass administrator emails are required.");
        }

        if (!configuration.GetValue<bool>("Operations:Monitoring:Enabled")
            || string.IsNullOrWhiteSpace(configuration["Operations:Monitoring:SecurityAlertSink"]))
        {
            errors.Add("Security monitoring and an alert sink are required.");
        }

        if (!DateTimeOffset.TryParse(
                configuration["Operations:Backup:RestoreVerifiedAtUtc"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var restoredAt)
            || restoredAt > now
            || now - restoredAt > MaximumRestoreVerificationAge)
        {
            errors.Add("A successful database restore verification from the last 90 days is required.");
        }
    }

    private static bool TryGetPublicHttpsUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            && string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && !IPAddress.TryParse(candidate.Host, out _)
            && !IsReservedExampleHost(candidate.Host)
            && string.IsNullOrEmpty(candidate.UserInfo))
        {
            uri = candidate;
            return true;
        }

        uri = null!;
        return false;
    }

    private static IReadOnlyList<string> Split(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(
                [',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsReservedExampleHost(string host)
    {
        return string.Equals(host, "example.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".example.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".example", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".test", StringComparison.OrdinalIgnoreCase);
    }
}
