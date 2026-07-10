namespace Emi.Qms.Api.ReviewSafe;

public static class ReviewSafeMode
{
    public const string ConfigurationKey = "ReviewSafe:Enabled";
    public const string EnvironmentKey = "REVIEW_SAFE_ENABLED";
    public const string DatabaseApplicationNameConfigurationKey = "ReviewSafe:DatabaseApplicationName";
    public const string DatabaseApplicationName = "emi-qms-uat-review";
    public const string MigrationCandidateDatabaseApplicationName = "emi-qms-uat-review-migration-candidate";
    public const string ErrorCode = "UatReviewReadOnly";
    public const string LockedMessage = "현재 UAT는 검수 전용 읽기 모드입니다. 저장, 삭제, 발송 또는 상태 변경을 수행할 수 없습니다.";

    public static bool IsEnabled(IConfiguration configuration)
    {
        return configuration.GetValue<bool>(ConfigurationKey)
            || configuration.GetValue<bool>(EnvironmentKey);
    }

    public static void ThrowIfInvalidActivation(IHostEnvironment environment, IConfiguration configuration)
    {
        if (!IsEnabled(configuration))
        {
            return;
        }

        if (environment.IsDevelopment()
            || string.Equals(environment.EnvironmentName, "UAT", StringComparison.OrdinalIgnoreCase))
        {
            _ = ResolveDatabaseApplicationName(configuration);
            return;
        }

        throw new InvalidOperationException(
            $"Review-safe UAT mode cannot be enabled in the '{environment.EnvironmentName}' environment.");
    }

    public static string ResolveDatabaseApplicationName(IConfiguration configuration)
    {
        var configured = configuration[DatabaseApplicationNameConfigurationKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DatabaseApplicationName;
        }

        return configured switch
        {
            DatabaseApplicationName => DatabaseApplicationName,
            MigrationCandidateDatabaseApplicationName => MigrationCandidateDatabaseApplicationName,
            _ => throw new InvalidOperationException(
                "Review-safe database application name is not in the code-reviewed allowlist.")
        };
    }
}
