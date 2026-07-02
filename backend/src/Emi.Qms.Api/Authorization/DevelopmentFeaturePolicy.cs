namespace Emi.Qms.Api.Authorization;

public sealed record DevelopmentFeatureDecision(
    bool IsExplicitlyEnabled,
    bool IsAllowedEnvironment,
    bool IsEnabled,
    string FeatureName)
{
    public bool IsInvalidActivationAttempt => IsExplicitlyEnabled && !IsAllowedEnvironment;
}

public static class DevelopmentFeaturePolicy
{
    public const string TestingEnvironmentName = "Testing";

    public static DevelopmentFeatureDecision EvaluateDevelopmentAuthentication(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        return Evaluate(
            environment,
            configuration,
            "development authentication",
            "DevAuthentication:Enabled",
            "DEV_AUTHENTICATION_ENABLED");
    }

    public static DevelopmentFeatureDecision EvaluateDevelopmentDataSeeding(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        return Evaluate(
            environment,
            configuration,
            "development data seeding",
            "DevelopmentData:SeedEnabled",
            "DEV_DATA_SEED_ENABLED");
    }

    public static DevelopmentFeatureDecision EvaluateAdminUserSwitch(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        return Evaluate(
            environment,
            configuration,
            "admin user switch",
            "AdminUserSwitch:Enabled",
            "ADMIN_USER_SWITCH_ENABLED",
            IsAdminUserSwitchAllowedEnvironment);
    }

    public static bool IsAllowedEnvironment(IHostEnvironment environment)
    {
        return environment.IsDevelopment()
            || environment.IsEnvironment(TestingEnvironmentName);
    }

    public static bool IsAdminUserSwitchAllowedEnvironment(IHostEnvironment environment)
    {
        return IsAllowedEnvironment(environment)
            || environment.IsEnvironment("UAT");
    }

    public static void ThrowIfInvalidActivation(DevelopmentFeatureDecision decision, IHostEnvironment environment)
    {
        if (!decision.IsInvalidActivationAttempt)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{decision.FeatureName} cannot be enabled when ASPNETCORE_ENVIRONMENT is {environment.EnvironmentName}. " +
            "Only Development and Testing are allowed.");
    }

    private static DevelopmentFeatureDecision Evaluate(
        IHostEnvironment environment,
        IConfiguration configuration,
        string featureName,
        string configurationKey,
        string environmentVariableKey,
        Func<IHostEnvironment, bool>? isAllowedEnvironment = null)
    {
        var configured = FirstConfiguredValue(configuration[environmentVariableKey], configuration[configurationKey]);
        var isExplicitlyEnabled = bool.TryParse(configured, out var configuredValue) && configuredValue;
        var environmentAllowed = (isAllowedEnvironment ?? IsAllowedEnvironment)(environment);

        return new DevelopmentFeatureDecision(
            isExplicitlyEnabled,
            environmentAllowed,
            isExplicitlyEnabled && environmentAllowed,
            featureName);
    }

    private static string? FirstConfiguredValue(string? primary, string? secondary)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        return string.IsNullOrWhiteSpace(secondary) ? null : secondary;
    }
}
