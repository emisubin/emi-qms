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
        "DATABASE_APPLY_MIGRATIONS_ON_STARTUP"
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

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
