namespace Emi.Qms.Api.Authorization;

public enum QmsAuthenticationMode
{
    Dev,
    EntraId
}

public static class QmsAuthenticationModePolicy
{
    public static QmsAuthenticationMode Resolve(IHostEnvironment environment, IConfiguration configuration)
    {
        var configured = FirstConfiguredValue(
            configuration["AUTH_MODE"],
            configuration["Authentication:Mode"]);

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return string.Equals(configured, "Dev", StringComparison.OrdinalIgnoreCase)
                ? QmsAuthenticationMode.Dev
                : QmsAuthenticationMode.EntraId;
        }

        return IsDevelopmentLikeEnvironment(environment)
            ? QmsAuthenticationMode.Dev
            : QmsAuthenticationMode.EntraId;
    }

    public static void ThrowIfInvalidConfiguration(IHostEnvironment environment, IConfiguration configuration)
    {
        var mode = Resolve(environment, configuration);
        var developmentAuth = DevelopmentFeaturePolicy.EvaluateDevelopmentAuthentication(environment, configuration);

        if (mode == QmsAuthenticationMode.Dev)
        {
            if (!IsDevelopmentLikeEnvironment(environment))
            {
                throw new InvalidOperationException(
                    $"Dev authentication mode cannot be used in '{environment.EnvironmentName}'. It is allowed only in Development or Testing.");
            }

            DevelopmentFeaturePolicy.ThrowIfInvalidActivation(developmentAuth, environment);
            return;
        }

        ThrowIfAzureAdMissing(configuration);
    }

    public static bool HasRequiredAzureAdConfiguration(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration["AzureAd:TenantId"])
            && !string.IsNullOrWhiteSpace(configuration["AzureAd:ClientId"])
            && !string.IsNullOrWhiteSpace(configuration["AzureAd:Instance"])
            && (!string.IsNullOrWhiteSpace(configuration["AzureAd:Audience"])
                || !string.IsNullOrWhiteSpace(configuration["AzureAd:ValidAudience"]));
    }

    private static void ThrowIfAzureAdMissing(IConfiguration configuration)
    {
        if (HasRequiredAzureAdConfiguration(configuration))
        {
            return;
        }

        throw new InvalidOperationException(
            "EntraId authentication requires AzureAd:TenantId, AzureAd:ClientId, AzureAd:Instance, and AzureAd:Audience or AzureAd:ValidAudience.");
    }

    private static string? FirstConfiguredValue(string? primary, string? secondary)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        return string.IsNullOrWhiteSpace(secondary) ? null : secondary;
    }

    private static bool IsDevelopmentLikeEnvironment(IHostEnvironment environment)
    {
        return environment.IsDevelopment()
            || string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }
}
