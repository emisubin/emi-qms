using System.Net;
using System.Text.RegularExpressions;
using Emi.Qms.Api.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class PublicDeploymentSecurityTests
{
    [Fact]
    public void ProductionPolicy_AcceptsCompleteSyntheticConfiguration()
    {
        var now = DateTimeOffset.Parse("2026-07-29T00:00:00Z");
        var configuration = Configuration(ValidProductionValues(now));

        var errors = ProductionSecurityPolicy.Evaluate(configuration, now);

        Assert.Empty(errors);
    }

    [Fact]
    public void ProductionPolicy_AcceptsBoundedTrustedProxyNetwork()
    {
        var now = DateTimeOffset.Parse("2026-07-29T00:00:00Z");
        var values = ValidProductionValues(now);
        values["ReverseProxy:KnownProxies"] = "";
        values["ReverseProxy:KnownNetworks"] = "10.42.0.0/23";

        var errors = ProductionSecurityPolicy.Evaluate(Configuration(values), now);

        Assert.Empty(errors);
    }

    [Fact]
    public void DatabaseOperationPolicy_AcceptsSplitLeastPrivilegedRoles()
    {
        var values = ValidDatabaseOperationValues();

        Assert.Empty(DatabaseOperationSecurityPolicy.Evaluate(
            Configuration(values),
            DatabaseOperationMode.RoleBootstrap));

        values["ConnectionStrings:QmsDatabase"] = values["ConnectionStrings:QmsDatabaseMigration"];
        values["Database:MigrationRoleName"] = DatabaseOperationSecurityPolicy.MigrationRoleName;
        values["Database:RuntimeRoleName"] = DatabaseOperationSecurityPolicy.RuntimeRoleName;
        Assert.Empty(DatabaseOperationSecurityPolicy.Evaluate(
            Configuration(values),
            DatabaseOperationMode.Migration));
    }

    [Fact]
    public void DatabaseOperationPolicy_RejectsSharedOrPrivilegedRuntimeConnections()
    {
        var values = ValidDatabaseOperationValues();
        values["ConnectionStrings:QmsDatabaseRuntime"] = values["ConnectionStrings:QmsDatabaseAdmin"];

        var bootstrapErrors = DatabaseOperationSecurityPolicy.Evaluate(
            Configuration(values),
            DatabaseOperationMode.RoleBootstrap);

        Assert.NotEmpty(bootstrapErrors);

        values["ConnectionStrings:QmsDatabase"] = values["ConnectionStrings:QmsDatabaseAdmin"];
        values["Database:MigrationRoleName"] = DatabaseOperationSecurityPolicy.MigrationRoleName;
        values["Database:RuntimeRoleName"] = DatabaseOperationSecurityPolicy.RuntimeRoleName;
        var migrationErrors = DatabaseOperationSecurityPolicy.Evaluate(
            Configuration(values),
            DatabaseOperationMode.Migration);

        Assert.Contains(migrationErrors, error =>
            error.Contains("fixed least-privileged role", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionPolicy_AllowsMigrationOnlyBeforeRestoreRehearsal()
    {
        var now = DateTimeOffset.Parse("2026-07-29T00:00:00Z");
        var values = ValidProductionValues(now);
        values["Operations:Backup:RestoreVerifiedAtUtc"] = "";

        var errors = ProductionSecurityPolicy.Evaluate(
            Configuration(values),
            now,
            requireRestoreVerification: false);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("127.0.0.0/8")]
    [InlineData("not-a-network")]
    public void ProductionPolicy_RejectsUnsafeTrustedProxyNetwork(string network)
    {
        var now = DateTimeOffset.Parse("2026-07-29T00:00:00Z");
        var values = ValidProductionValues(now);
        values["ReverseProxy:KnownProxies"] = "";
        values["ReverseProxy:KnownNetworks"] = network;

        var errors = ProductionSecurityPolicy.Evaluate(Configuration(values), now);

        Assert.Contains(errors, error =>
            error.Contains("proxy IP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProductionStartup_FailsClosedWhenSecurityReadinessIsMissing()
    {
        using var factory = QmsWebApplicationFactory.Create(
            "Production",
            new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = "EntraId",
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
                ["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222",
                ["AzureAd:Audience"] = "api://22222222-2222-2222-2222-222222222222"
            });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(
            "Production security configuration is incomplete",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidProductionConfigurations))]
    public void ProductionPolicy_RejectsUnsafeConfiguration(
        string key,
        string? value,
        string expectedError)
    {
        var now = DateTimeOffset.Parse("2026-07-29T00:00:00Z");
        var values = ValidProductionValues(now);
        values[key] = value;

        var errors = ProductionSecurityPolicy.Evaluate(Configuration(values), now);

        Assert.Contains(errors, error =>
            error.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Responses_IncludeBrowserSecurityHeaders()
    {
        using var factory = QmsWebApplicationFactory.Create(
            "Testing",
            new Dictionary<string, string?> { ["RateLimiting:Enabled"] = "true" });
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(
            "strict-origin-when-cross-origin",
            response.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(response.Headers.Contains("Permissions-Policy"));
        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Private);
        Assert.True(cacheControl.NoStore);
        Assert.Equal(TimeSpan.Zero, cacheControl.MaxAge);
        Assert.Contains(
            response.Headers.Pragma,
            directive => string.Equals(
                directive.Name,
                "no-cache",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProductionArtifacts_UseImmutableExternalReferences()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dockerfiles = new[]
        {
            Path.Combine(repositoryRoot, "backend", "Dockerfile.production"),
            Path.Combine(repositoryRoot, "frontend", "Dockerfile.production")
        };

        var dockerfileImageReferences = dockerfiles
            .SelectMany(File.ReadLines)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("FROM ", StringComparison.Ordinal))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);
        var productionComposePath = Path.Combine(
            repositoryRoot,
            "infrastructure",
            "docker-compose.production.yml");
        var composeImageReferences = File.ReadLines(productionComposePath)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("image: ", StringComparison.Ordinal))
            .Select(line => line["image: ".Length..]);

        Assert.All(
            dockerfileImageReferences.Concat(composeImageReferences),
            reference => Assert.Matches(
                @"^[^@\s]+@sha256:[0-9a-f]{64}$",
                reference));

        var workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml");
        var workflowLines = File.ReadAllLines(workflowPath);
        var actionReferences = workflowLines
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- uses: actions/", StringComparison.Ordinal))
            .Select(line => line.Split('#', 2)[0]["- uses: ".Length..].Trim());

        Assert.NotEmpty(actionReferences);
        Assert.All(
            actionReferences,
            reference => Assert.Matches(
                @"^actions/[^@\s]+@[0-9a-f]{40}$",
                reference));

        var postgresReference = workflowLines
            .Select(line => line.Trim())
            .Single(line => line.StartsWith("image: postgres:", StringComparison.Ordinal))
            ["image: ".Length..];
        Assert.Matches(
            @"^postgres:[^@\s]+@sha256:[0-9a-f]{64}$",
            postgresReference);
        Assert.Contains("permissions:", workflowLines);
        Assert.Equal(
            actionReferences.Count(reference =>
                reference.StartsWith("actions/checkout@", StringComparison.Ordinal)),
            workflowLines.Count(line => line.Trim() == "persist-credentials: false"));
    }

    [Fact]
    public void ProductionArtifacts_RequireSplitEntraClientsAndIndependentMigration()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(
            Path.Combine(repositoryRoot, "infrastructure", "docker-compose.production.yml"));
        var frontendDockerfile = File.ReadAllText(
            Path.Combine(repositoryRoot, "frontend", "Dockerfile.production"));
        var backendDockerfile = File.ReadAllText(
            Path.Combine(repositoryRoot, "backend", "Dockerfile.production"));
        var program = File.ReadAllText(
            Path.Combine(repositoryRoot, "backend", "src", "Emi.Qms.Api", "Program.cs"));

        Assert.Contains("${ENTRA_API_CLIENT_ID:", compose, StringComparison.Ordinal);
        Assert.Contains("${ENTRA_SPA_CLIENT_ID:", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("${ENTRA_CLIENT_ID:", compose, StringComparison.Ordinal);
        Assert.Contains("AzureAd__SpaClientId", compose, StringComparison.Ordinal);
        Assert.Contains("VITE_AZURE_API_CLIENT_ID", frontendDockerfile, StringComparison.Ordinal);
        Assert.Contains("client === apiClient", frontendDockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY database/migrations database/migrations", backendDockerfile, StringComparison.Ordinal);
        Assert.Contains("command:\n      - --migrate-only", compose, StringComparison.Ordinal);
        Assert.Contains("--migrate-only", program, StringComparison.Ordinal);
    }

    [Fact]
    public void AzureFrontend_RequiresFrontDoorIdentityAndOriginVerification()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dockerfile = File.ReadAllText(
            Path.Combine(repositoryRoot, "frontend", "Dockerfile.azure"));
        var nginx = File.ReadAllText(
            Path.Combine(repositoryRoot, "infrastructure", "azure-pilot", "nginx.conf.template"));

        Assert.Contains("@sha256:", dockerfile, StringComparison.Ordinal);
        Assert.Contains("EXPOSE 8080", dockerfile, StringComparison.Ordinal);
        Assert.Contains("VITE_AZURE_API_CLIENT_ID", dockerfile, StringComparison.Ordinal);
        Assert.Contains("client === apiClient", dockerfile, StringComparison.Ordinal);
        Assert.Contains("$http_x_azure_fdid", nginx, StringComparison.Ordinal);
        Assert.Contains("$http_x_pms_origin_verify", nginx, StringComparison.Ordinal);
        Assert.Contains("return 403;", nginx, StringComparison.Ordinal);
        Assert.Contains("$http_x_azure_clientip", nginx, StringComparison.Ordinal);
        Assert.Contains("proxy_set_header Host ${BACKEND_FQDN};", nginx, StringComparison.Ordinal);
        Assert.Contains("proxy_set_header X-Forwarded-Host ${PUBLIC_HOST};", nginx, StringComparison.Ordinal);
        Assert.DoesNotContain("proxy_set_header Host ${PUBLIC_HOST};", nginx, StringComparison.Ordinal);
        Assert.DoesNotContain("ssl_certificate", nginx, StringComparison.Ordinal);
    }

    [Fact]
    public void AzureArtifacts_UseSplitIdentitiesAndSecretScopedKeyVaultAccess()
    {
        var repositoryRoot = FindRepositoryRoot();
        var azureRoot = Path.Combine(repositoryRoot, "infrastructure", "azure-pilot");
        var foundation = File.ReadAllText(Path.Combine(azureRoot, "foundation.bicep"));
        var identityAccess = File.ReadAllText(Path.Combine(azureRoot, "identity-access.bicep"));
        var workloads = File.ReadAllText(Path.Combine(azureRoot, "workloads.bicep"));

        foreach (var identity in new[]
        {
            "backendIdentity",
            "frontendIdentity",
            "migrationIdentity",
            "databaseBootstrapIdentity"
        })
        {
            Assert.Contains(identity, foundation, StringComparison.Ordinal);
            Assert.Contains(identity, identityAccess, StringComparison.Ordinal);
            Assert.Contains(identity, workloads, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("runtimeIdentity", foundation, StringComparison.Ordinal);
        Assert.DoesNotContain("runtimeIdentity", workloads, StringComparison.Ordinal);
        Assert.DoesNotContain("scope: keyVault\n", identityAccess, StringComparison.Ordinal);
        Assert.Equal(
            14,
            Regex.Matches(identityAccess, @"scope: \w+Secret\r?$", RegexOptions.Multiline).Count);

        Assert.Contains("database-runtime-connection-string", workloads, StringComparison.Ordinal);
        Assert.Contains("database-migration-connection-string", workloads, StringComparison.Ordinal);
        Assert.Contains("database-admin-connection-string", workloads, StringComparison.Ordinal);
        Assert.Contains("--bootstrap-database-roles", workloads, StringComparison.Ordinal);
        Assert.Contains("--migrate-only", workloads, StringComparison.Ordinal);
        Assert.Contains("value: 'pms_migrator'", workloads, StringComparison.Ordinal);
        Assert.Contains("value: 'pms_app'", workloads, StringComparison.Ordinal);
        Assert.Contains(
            "var backendInternalHost = 'backend.internal.${containerAppsEnvironment.properties.defaultDomain}'",
            workloads,
            StringComparison.Ordinal);
        Assert.Contains("value: '${publicHost};${backendInternalHost}'", workloads, StringComparison.Ordinal);
        Assert.DoesNotContain("name: 'AllowedHosts'\n    value: '*'", workloads, StringComparison.Ordinal);
        Assert.DoesNotContain("database-connection-string", workloads, StringComparison.Ordinal);
        Assert.Contains("entra-access-gate-client-secret", workloads, StringComparison.Ordinal);
        Assert.Contains("web-push-vapid-public-key", workloads, StringComparison.Ordinal);
        Assert.Contains("web-push-vapid-private-key", workloads, StringComparison.Ordinal);
        Assert.Contains("development-operator-emails", workloads, StringComparison.Ordinal);
        Assert.Contains(
            "name: 'Authentication__DevelopmentOperatorEmails'\n    secretRef: 'development-ops'",
            workloads,
            StringComparison.Ordinal);
        Assert.DoesNotContain("name: 'development-operator-emails'", workloads, StringComparison.Ordinal);
        Assert.Contains("frontendAccessGateRoleAssignmentName", identityAccess, StringComparison.Ordinal);
        Assert.Contains("backendWebPushVapidPublicKeyRoleAssignmentName", identityAccess, StringComparison.Ordinal);
        Assert.Contains("backendWebPushVapidPrivateKeyRoleAssignmentName", identityAccess, StringComparison.Ordinal);
        Assert.Contains(
            "name: 'Notifications__WebPush__Enabled'\n    value: enabled",
            workloads,
            StringComparison.Ordinal);
        Assert.Contains(
            "name: 'Notifications__WebPush__DryRun'\n    value: disabled",
            workloads,
            StringComparison.Ordinal);
        Assert.Contains(
            "name: 'Notifications__WebPush__PublicKey'\n    secretRef: 'web-push-vapid-public-key'",
            workloads,
            StringComparison.Ordinal);
        Assert.Contains(
            "name: 'Notifications__WebPush__PrivateKey'\n    secretRef: 'web-push-vapid-private-key'",
            workloads,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AzureFrontend_RequiresEntraPreAuthenticationBeforeServingApplicationAssets()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workloads = File.ReadAllText(
            Path.Combine(repositoryRoot, "infrastructure", "azure-pilot", "workloads.bicep"));

        Assert.Contains(
            "resource frontendAuth 'Microsoft.App/containerApps/authConfigs@2024-03-01'",
            workloads,
            StringComparison.Ordinal);
        Assert.Contains("unauthenticatedClientAction: 'RedirectToLoginPage'", workloads, StringComparison.Ordinal);
        Assert.Contains("redirectToProvider: 'azureactivedirectory'", workloads, StringComparison.Ordinal);
        Assert.Contains("convention: 'Standard'", workloads, StringComparison.Ordinal);
        Assert.Contains("clientSecretSettingName: 'entra-access-gate-client-secret'", workloads, StringComparison.Ordinal);
        Assert.Contains("allowedAudiences: [", workloads, StringComparison.Ordinal);
        Assert.Contains("'/health/live'", workloads, StringComparison.Ordinal);
        Assert.DoesNotContain("'/assets", workloads, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowAnonymous", workloads, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionNginx_PreservesSecurityHeaderInheritanceForAssets()
    {
        var repositoryRoot = FindRepositoryRoot();
        var nginx = File.ReadAllText(
            Path.Combine(repositoryRoot, "infrastructure", "production", "nginx.conf.template"));
        var assetsLocation = Regex.Match(
            nginx,
            @"location /assets/ \{(?<body>.*?)^\s*\}",
            RegexOptions.Multiline | RegexOptions.Singleline);
        var shellLocation = Regex.Match(
            nginx,
            @"location / \{(?<body>.*?)^\s*\}",
            RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.True(assetsLocation.Success);
        Assert.Contains("expires 1y;", assetsLocation.Groups["body"].Value);
        Assert.DoesNotContain("add_header", assetsLocation.Groups["body"].Value);
        Assert.True(shellLocation.Success);
        Assert.Contains("expires -1;", shellLocation.Groups["body"].Value);
    }

    [Fact]
    public async Task HostFiltering_RejectsUnknownHost()
    {
        using var factory = QmsWebApplicationFactory.Create("Testing");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Host = "attacker.invalid";

        var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RateLimiting_Returns429AndRetryAfter()
    {
        using var factory = QmsWebApplicationFactory.Create(
            "Testing",
            new Dictionary<string, string?>
            {
                ["RateLimiting:Enabled"] = "true",
                ["RateLimiting:HealthRequestsPerMinute"] = "1"
            });
        using var client = factory.CreateClient();

        var first = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);
        var second = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.Contains("Retry-After"));
    }

    [Theory]
    [InlineData(UploadMalwareScanStatus.Infected, HttpStatusCode.UnprocessableEntity)]
    [InlineData(UploadMalwareScanStatus.Unavailable, HttpStatusCode.ServiceUnavailable)]
    public async Task UploadSecurity_BlocksUnsafeOrUnscannableFiles(
        UploadMalwareScanStatus status,
        HttpStatusCode expectedStatus)
    {
        using var factory = UploadFactory(status);
        using var client = CreateUploadClient(factory);
        using var content = Multipart([1, 2, 3, 4]);

        var response = await client.PostAsync(
            "/missing-upload-target",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task UploadSecurity_AllowsCleanFileToReachEndpointRouting()
    {
        using var factory = UploadFactory(UploadMalwareScanStatus.Clean);
        using var client = CreateUploadClient(factory);
        using var content = Multipart([1, 2, 3, 4]);

        var response = await client.PostAsync(
            "/missing-upload-target",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadSecurity_BlocksJpegExifBeforeMalwareScanner()
    {
        var scanner = new FixedUploadMalwareScanner(UploadMalwareScanStatus.Clean);
        using var factory = UploadFactory(scanner);
        using var client = CreateUploadClient(factory);
        byte[] jpegWithExif =
        [
            0xff, 0xd8,
            0xff, 0xe1, 0x00, 0x08,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            0xff, 0xd9
        ];
        using var content = Multipart(jpegWithExif);

        var response = await client.PostAsync(
            "/missing-upload-target",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, scanner.CallCount);
    }

    [Fact]
    public async Task UploadSecurity_InspectsPngMetadataBeyondFirstMegabyte()
    {
        var scanner = new FixedUploadMalwareScanner(UploadMalwareScanStatus.Clean);
        using var factory = UploadFactory(scanner);
        using var client = CreateUploadClient(factory);
        const int dataLength = 1024 * 1024;
        byte[] png = new byte[8 + 8 + dataLength + 4 + 8];
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        signature.CopyTo(png, 0);
        png[8] = 0x00;
        png[9] = 0x10;
        png[10] = 0x00;
        png[11] = 0x00;
        "IDAT"u8.CopyTo(png.AsSpan(12, 4));
        var metadataOffset = 8 + 8 + dataLength + 4;
        "tEXt"u8.CopyTo(png.AsSpan(metadataOffset + 4, 4));
        using var content = Multipart(png);

        var response = await client.PostAsync(
            "/missing-upload-target",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, scanner.CallCount);
    }

    public static TheoryData<string, string?, string> InvalidProductionConfigurations()
    {
        return new TheoryData<string, string?, string>
        {
            { "AllowedHosts", "*", "exact production DNS" },
            { "Frontend:Origin", "http://qms.example.com", "public HTTPS origin" },
            { "FRONTEND_ORIGIN", "http://attacker.invalid", "public HTTPS origin" },
            { "Frontend:RedirectUri", "https://other.example.com", "exact Frontend:Origin" },
            { "ReverseProxy:KnownProxies", "", "proxy IP" },
            { "AzureAd:Domain", "", "Entra tenant" },
            { "AzureAd:SpaClientId", "", "distinct API and SPA" },
            { "AzureAd:SpaClientId", "22222222-2222-2222-2222-222222222222", "distinct API and SPA" },
            { "RateLimiting:Enabled", "false", "Rate limiting" },
            { "RateLimiting:ReadRequestsPerMinute", "999999", "Rate limiting" },
            { "UploadSecurity:Enabled", "false", "malware scanning" },
            { "UploadSecurity:RejectImageMetadata", "false", "malware scanning" },
            {
                "ConnectionStrings:QmsDatabase",
                "Host=db.example.com;Database=emi;Username=app;Password=test;SSL Mode=Require",
                "VerifyFull"
            },
            {
                "ConnectionStrings:QmsDatabase",
                "Host=db.example.com;Database=emi;Username=administrator;Password=test;SSL Mode=VerifyFull",
                "runtime role"
            },
            { "Authentication:BootstrapAdminEmails", "one@example.com", "two distinct" },
            { "AUTHENTICATION_BOOTSTRAP_ADMIN_EMAILS", "one@example.com", "two distinct" },
            { "Operations:Monitoring:Enabled", "false", "Security monitoring" },
            { "Operations:Backup:RestoreVerifiedAtUtc", "2025-01-01T00:00:00Z", "last 90 days" }
        };
    }

    private static Dictionary<string, string?> ValidProductionValues(DateTimeOffset now)
    {
        return new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "qms.emi.co.kr",
            ["Frontend:Origin"] = "https://qms.emi.co.kr",
            ["Frontend:RedirectUri"] = "https://qms.emi.co.kr",
            ["ReverseProxy:KnownProxies"] = "172.30.0.10",
            ["Authentication:Mode"] = "EntraId",
            ["Authentication:BootstrapAdminEmails"] =
                "breakglass-one@example.com;breakglass-two@example.com",
            ["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["AzureAd:SpaClientId"] = "33333333-3333-3333-3333-333333333333",
            ["AzureAd:Audience"] = "api://22222222-2222-2222-2222-222222222222",
            ["AzureAd:Domain"] = "emi.co.kr",
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:ReadRequestsPerMinute"] = "3000",
            ["RateLimiting:MutationRequestsPerMinute"] = "600",
            ["RateLimiting:UploadRequestsPerMinute"] = "60",
            ["RateLimiting:HealthRequestsPerMinute"] = "600",
            ["UploadSecurity:Enabled"] = "true",
            ["UploadSecurity:FailClosed"] = "true",
            ["UploadSecurity:ScannerHost"] = "clamav",
            ["UploadSecurity:ScannerPort"] = "3310",
            ["UploadSecurity:TimeoutSeconds"] = "20",
            ["UploadSecurity:MaximumFileBytes"] = "33554432",
            ["UploadSecurity:RejectImageMetadata"] = "true",
            ["ConnectionStrings:QmsDatabase"] =
                "Host=db.example.com;Database=emi;Username=pms_app;Password=test;SSL Mode=VerifyFull",
            ["Database:RuntimeRoleName"] = "pms_app",
            ["Operations:Monitoring:Enabled"] = "true",
            ["Operations:Monitoring:SecurityAlertSink"] = "synthetic-security-sink",
            ["Operations:Backup:RestoreVerifiedAtUtc"] =
                now.AddDays(-1).ToString("O")
        };
    }

    private static Dictionary<string, string?> ValidDatabaseOperationValues()
    {
        static string Connection(string username, char passwordCharacter)
        {
            return new NpgsqlConnectionStringBuilder
            {
                Host = "pms-postgres.example.org",
                Port = 5432,
                Database = "emi_qms",
                Username = username,
                Password = new string(passwordCharacter, 40),
                SslMode = SslMode.VerifyFull
            }.ConnectionString;
        }

        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:QmsDatabaseAdmin"] = Connection("pms_admin", 'A'),
            ["ConnectionStrings:QmsDatabaseMigration"] = Connection(
                DatabaseOperationSecurityPolicy.MigrationRoleName,
                'M'),
            ["ConnectionStrings:QmsDatabaseRuntime"] = Connection(
                DatabaseOperationSecurityPolicy.RuntimeRoleName,
                'R')
        };
    }

    private static IConfiguration Configuration(
        IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static QmsWebApplicationFactory UploadFactory(UploadMalwareScanStatus status)
    {
        return UploadFactory(new FixedUploadMalwareScanner(status));
    }

    private static QmsWebApplicationFactory UploadFactory(IUploadMalwareScanner scanner)
    {
        return QmsWebApplicationFactory.Create(
            "Testing",
            new Dictionary<string, string?>
            {
                ["UploadSecurity:Enabled"] = "true",
                ["UploadSecurity:FailClosed"] = "true",
                ["UploadSecurity:RejectImageMetadata"] = "true"
            },
            includeDefaultDevelopmentAuthentication: true,
            configureTestServices: services =>
            {
                var descriptor = services.Single(
                    service => service.ServiceType == typeof(IUploadMalwareScanner));
                services.Remove(descriptor);
                services.AddSingleton(scanner);
            });
    }

    private static HttpClient CreateUploadClient(QmsWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "dev-admin");
        return client;
    }

    private static MultipartFormDataContent Multipart(byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", "upload.bin");
        return content;
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

    private sealed class FixedUploadMalwareScanner(UploadMalwareScanStatus status)
        : IUploadMalwareScanner
    {
        public int CallCount { get; private set; }

        public Task<UploadMalwareScanResult> ScanAsync(
            Stream content,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new UploadMalwareScanResult(status, status.ToString()));
        }
    }
}
