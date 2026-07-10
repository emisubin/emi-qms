using Microsoft.Extensions.Configuration;

namespace Emi.Qms.Api.Tests;

internal static class TestConfigurationIsolation
{
    private static readonly HashSet<string> TestControlledKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "DevAuthentication:Enabled",
        "DEV_AUTHENTICATION_ENABLED",
        "DevelopmentData:SeedEnabled",
        "DEV_DATA_SEED_ENABLED",
        "Database:ApplyMigrationsOnStartup",
        "DATABASE_APPLY_MIGRATIONS_ON_STARTUP",
        "ReviewSafe:Enabled",
        "REVIEW_SAFE_ENABLED"
    };

    public static IConfiguration BuildBaseDatabaseConfiguration(IReadOnlyDictionary<string, string?> dotEnvValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(dotEnvValues)
            .AddEnvironmentVariables()
            .Build();

        var values = configuration.AsEnumerable()
            .Where(item => item.Value is not null && !TestControlledKeys.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        AddDefaultIfMissing(values, "DATABASE_HOST", "localhost");
        AddDefaultIfMissing(values, "DATABASE_PORT", "5432");
        AddDefaultIfMissing(values, "DATABASE_NAME", "emi_qms_dev");
        AddDefaultIfMissing(values, "DATABASE_USER", "emi_qms");
        AddDefaultIfMissing(values, "DATABASE_PASSWORD", "local_only_change_me");

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void AddDefaultIfMissing(IDictionary<string, string?> values, string key, string value)
    {
        if (!values.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing))
        {
            values[key] = value;
        }
    }
}
