using System.Net;
using System.Net.Http.Json;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.ProductionPlanning;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class ReviewSafeModeTests
{
    private static readonly IReadOnlyDictionary<string, string?> ReviewConfiguration =
        new Dictionary<string, string?>
        {
            ["ReviewSafe:Enabled"] = "true",
            ["DevAuthentication:Enabled"] = "true",
            ["DevelopmentData:SeedEnabled"] = "false",
            ["Database:ApplyMigrationsOnStartup"] = "false"
        };

    [Fact]
    public void Activation_IsDisabledByDefault_AndRejectedOutsideDevelopmentOrUat()
    {
        var disabled = new ConfigurationBuilder().Build();
        Assert.False(ReviewSafeMode.IsEnabled(disabled));

        var enabled = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ReviewSafe:Enabled"] = "true" })
            .Build();
        var production = new TestHostEnvironment { EnvironmentName = Environments.Production };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ReviewSafeMode.ThrowIfInvalidActivation(production, enabled));
        Assert.Contains("cannot be enabled", exception.Message, StringComparison.OrdinalIgnoreCase);

        ReviewSafeMode.ThrowIfInvalidActivation(
            new TestHostEnvironment { EnvironmentName = Environments.Development },
            enabled);
        ReviewSafeMode.ThrowIfInvalidActivation(
            new TestHostEnvironment { EnvironmentName = "UAT" },
            enabled);

        var invalidApplicationName = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReviewSafe:Enabled"] = "true",
                ["ReviewSafe:DatabaseApplicationName"] = "arbitrary-review-name"
            })
            .Build();
        Assert.Throws<InvalidOperationException>(() => ReviewSafeMode.ThrowIfInvalidActivation(
            new TestHostEnvironment { EnvironmentName = Environments.Development },
            invalidApplicationName));
    }

    [Fact]
    public async Task MutationMiddleware_BlocksUnsafeMethodsAndMethodOverrideBeforeEndpointExecution()
    {
        await using var factory = QmsWebApplicationFactory.Create(
            Environments.Development,
            ReviewConfiguration,
            includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();

        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, "/api/projects?review=true")
            {
                Content = JsonContent.Create(new { ignored = true })
            };
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal((HttpStatusCode)423, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ReviewSafeLockedResponse>(TestContext.Current.CancellationToken);
            Assert.Equal(ReviewSafeMode.ErrorCode, body?.ErrorCode);
            Assert.Equal(ReviewSafeMode.LockedMessage, body?.Message);
        }

        using var overrideRequest = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        overrideRequest.Headers.Add("X-HTTP-Method-Override", "DELETE");
        using var overrideResponse = await client.SendAsync(overrideRequest, TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)423, overrideResponse.StatusCode);

        using var getResponse = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task RuntimeEndpoint_ReportsAuthoritativeFailClosedReviewStateWithoutSecrets()
    {
        await using var factory = QmsWebApplicationFactory.Create(
            Environments.Development,
            ReviewConfiguration,
            includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/runtime-mode", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var status = await response.Content.ReadFromJsonAsync<ReviewSafeRuntimeStatus>(TestContext.Current.CancellationToken);

        Assert.NotNull(status);
        Assert.Equal("ReviewSafe", status.Mode);
        Assert.True(status.ReviewSafe);
        Assert.False(status.MutationAllowed);
        Assert.False(status.BackgroundWorkersEnabled);
        Assert.False(status.NotificationDeliveryWorkerEnabled);
        Assert.False(status.NotificationEscalationWorkerEnabled);
        Assert.False(status.AdminDeletionPurgeWorkerEnabled);
        Assert.False(status.MutationWorkersEnabled);
        Assert.False(status.ExternalProvidersEnabled);
        Assert.False(status.MigrationExecutionEnabled);
        Assert.False(status.Ready);
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);

        using var readyResponse = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
        Assert.Contains(
            "database_not_configured",
            await readyResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReviewSafeRegistration_ExcludesMutationWorkersHandlersAndActualProviders()
    {
        await using var factory = QmsWebApplicationFactory.Create(
            Environments.Development,
            ReviewConfiguration,
            includeDefaultDevelopmentAuthentication: true);
        _ = factory.Services;

        var hostedServices = factory.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();
        Assert.DoesNotContain(typeof(NotificationDeliveryWorker), hostedServices);
        Assert.DoesNotContain(typeof(NotificationEscalationWorker), hostedServices);
        Assert.DoesNotContain(typeof(AdminDeletionPurgeWorker), hostedServices);
        Assert.Empty(factory.Services.GetServices<INotificationChannelHandler>());
        Assert.Empty(factory.Services.GetServices<ITeamsWebhookClient>());
        Assert.Empty(factory.Services.GetServices<ITeamsActivityClient>());
        Assert.Empty(factory.Services.GetServices<IMailClient>());
        Assert.IsType<ReviewSafeKoreanHolidayProvider>(factory.Services.GetRequiredService<IKoreanHolidayProvider>());
    }

    [Fact]
    public async Task DevelopmentRegistration_PreservesExistingWorkersAndProviders()
    {
        await using var factory = QmsWebApplicationFactory.Create(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["ReviewSafe:Enabled"] = "false",
                ["DevAuthentication:Enabled"] = "true",
                ["DevelopmentData:SeedEnabled"] = "false",
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["Notifications:Dispatch:Enabled"] = "true",
                ["Notifications:Escalation:Enabled"] = "true",
                ["AdminDeletionPurge:Enabled"] = "true"
            },
            includeDefaultDevelopmentAuthentication: true);
        _ = factory.Services;

        var hostedServices = factory.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();
        Assert.Contains(typeof(NotificationDeliveryWorker), hostedServices);
        Assert.Contains(typeof(NotificationEscalationWorker), hostedServices);
        Assert.Contains(typeof(AdminDeletionPurgeWorker), hostedServices);
        Assert.Equal(4, factory.Services.GetServices<INotificationChannelHandler>().Count());
        Assert.IsType<OfficialKoreanHolidayProvider>(factory.Services.GetRequiredService<IKoreanHolidayProvider>());
    }

    [Fact]
    public async Task MigrationAndSeedDirectCalls_FailClosedInReviewSafeMode()
    {
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Development,
            ContentRootPath = FindRepositoryRoot()
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(ReviewConfiguration)
            .Build();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);
        var runner = new DatabaseMigrationRunner(
            connectionStringProvider,
            new DatabaseMigrationCatalog(environment),
            configuration,
            NullLogger<DatabaseMigrationRunner>.Instance);
        var seeder = new DevelopmentIdentitySeeder(
            connectionStringProvider,
            configuration,
            environment,
            NullLogger<DevelopmentIdentitySeeder>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.ApplyAsync(TestContext.Current.CancellationToken));
        Assert.False(seeder.IsEnabled());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ConnectionStringProvider_EnforcesReadOnlyAndReviewApplicationName()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReviewSafe:Enabled"] = "true",
                ["ConnectionStrings:QmsDatabase"] = "Host=localhost;Port=5432;Database=emi_qms_e2e_guard;Username=test;Password=placeholder"
            })
            .Build();

        var value = new DatabaseConnectionStringProvider(configuration).GetConnectionString();
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(value);
        Assert.Equal(ReviewSafeMode.DatabaseApplicationName, builder.ApplicationName);
        Assert.Equal("-c default_transaction_read_only=on", builder.Options);

        var candidateConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReviewSafe:Enabled"] = "true",
                ["ReviewSafe:DatabaseApplicationName"] = ReviewSafeMode.MigrationCandidateDatabaseApplicationName,
                ["ConnectionStrings:QmsDatabase"] = "Host=localhost;Port=5432;Database=emi_qms_e2e_guard;Username=test;Password=placeholder"
            })
            .Build();
        var candidate = new Npgsql.NpgsqlConnectionStringBuilder(
            new DatabaseConnectionStringProvider(candidateConfiguration).GetConnectionString());
        Assert.Equal(ReviewSafeMode.MigrationCandidateDatabaseApplicationName, candidate.ApplicationName);
    }

    [Fact]
    public async Task MigrationCatalog_RejectsDuplicateAndMissingPrefixes()
    {
        var duplicateDirectory = Directory.CreateTempSubdirectory("emi-qms-migration-catalog-duplicate-");
        var missingDirectory = Directory.CreateTempSubdirectory("emi-qms-migration-catalog-missing-");
        try
        {
            File.WriteAllText(Path.Combine(duplicateDirectory.FullName, "0001_first.sql"), "select 1;");
            File.WriteAllText(Path.Combine(duplicateDirectory.FullName, "0001_second.sql"), "select 1;");
            var duplicate = Assert.Throws<MigrationCatalogException>(
                () => DatabaseMigrationCatalog.FromPath(duplicateDirectory.FullName).GetSnapshot());
            Assert.Equal("migration_catalog_duplicate_prefix", duplicate.Reason);
            var duplicateStatus = await CreateCatalogFailureStatusAsync(duplicateDirectory.FullName);
            Assert.Equal("migration_catalog_invalid", duplicateStatus.Reason);
            Assert.Equal(MigrationLedgerInspector.MismatchStatus, duplicateStatus.MigrationLedgerStatus);

            File.WriteAllText(Path.Combine(missingDirectory.FullName, "0001_first.sql"), "select 1;");
            File.WriteAllText(Path.Combine(missingDirectory.FullName, "0003_third.sql"), "select 1;");
            var missing = Assert.Throws<MigrationCatalogException>(
                () => DatabaseMigrationCatalog.FromPath(missingDirectory.FullName).GetSnapshot());
            Assert.Equal("migration_catalog_missing_prefix", missing.Reason);
            var missingStatus = await CreateCatalogFailureStatusAsync(missingDirectory.FullName);
            Assert.Equal("migration_catalog_invalid", missingStatus.Reason);
            Assert.Equal(MigrationLedgerInspector.MismatchStatus, missingStatus.MigrationLedgerStatus);
        }
        finally
        {
            duplicateDirectory.Delete(recursive: true);
            missingDirectory.Delete(recursive: true);
        }
    }

    private static Task<ReviewSafeRuntimeStatus> CreateCatalogFailureStatusAsync(string migrationsPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ReviewSafe:Enabled"] = "true" })
            .Build();
        var catalog = DatabaseMigrationCatalog.FromPath(migrationsPath);
        var service = new ReviewSafeStatusService(
            new DatabaseConnectionStringProvider(configuration),
            catalog,
            new MigrationLedgerInspector(catalog),
            configuration,
            new TestHostEnvironment { EnvironmentName = Environments.Development });
        return service.CheckAsync(TestContext.Current.CancellationToken);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "database", "migrations")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Emi.Qms.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
