using Emi.Qms.Api.DeploymentMaintenance;
using Emi.Qms.Api.InteriorBusbar;
using Emi.Qms.Api;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Audit;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Calendar;
using Emi.Qms.Api.DataExports;
using Emi.Qms.Api.Home;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Logistics;
using Emi.Qms.Api.Manufacturing;
using Emi.Qms.Api.Materials;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.Notices;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.PanelInformation;
using Emi.Qms.Api.PanelQr;
using Emi.Qms.Api.Pending;
using Emi.Qms.Api.PendingTypes;
using Emi.Qms.Api.Procurement;
using Emi.Qms.Api.ProductionPlanning;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.QualityInspections;
using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Sales;
using Emi.Qms.Api.Security;
using Emi.Qms.Api.Ul891Sets;
using Emi.Qms.Api.Workflow;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HostFiltering;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

ReviewSafeMode.ThrowIfInvalidActivation(builder.Environment, builder.Configuration);
var reviewSafeEnabled = ReviewSafeMode.IsEnabled(builder.Configuration);
var mutationWorkerActivation = MutationWorkerActivationPolicy.Evaluate(builder.Configuration, reviewSafeEnabled);
var uploadSecurityConfiguration = builder.Configuration
    .GetSection(UploadSecurityOptions.SectionName)
    .Get<UploadSecurityOptions>()
    ?? new UploadSecurityOptions();

builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

var businessUnitConfiguration = BusinessUnitConfiguration.Read(builder.Configuration);
businessUnitConfiguration.ThrowIfInvalid();

builder.Services.AddHostFiltering(options =>
{
    var hosts = builder.Configuration["AllowedHosts"]
        ?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? ["localhost", "127.0.0.1"];
    options.AllowedHosts = hosts.Length == 0 ? ["localhost", "127.0.0.1"] : hosts;
    options.AllowEmptyHosts = false;
    options.IncludeFailureMessage = false;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();

    foreach (var address in TrustedProxyConfiguration.ReadKnownProxies(builder.Configuration))
    {
        options.KnownProxies.Add(address);
    }

    foreach (var network in TrustedProxyConfiguration.ReadKnownNetworks(builder.Configuration))
    {
        options.KnownIPNetworks.Add(network);
    }
});
builder.Services.AddHsts(options =>
{
    options.IncludeSubDomains = true;
    options.Preload = true;
    options.MaxAge = TimeSpan.FromDays(365);
});
builder.Services.AddQmsRateLimiting(builder.Configuration);
builder.Services.Configure<UploadSecurityOptions>(
    builder.Configuration.GetSection(UploadSecurityOptions.SectionName));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        Math.Max(OsanProgressPhotoValidator.MaximumMultipartBytes, uploadSecurityConfiguration.MaximumFileBytes + (2 * 1024 * 1024));
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        Math.Max(OsanProgressPhotoValidator.MaximumMultipartBytes, uploadSecurityConfiguration.MaximumFileBytes + (2 * 1024 * 1024));
});
builder.Services.AddSingleton<IUploadMalwareScanner, ClamAvUploadMalwareScanner>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDevelopment", policy =>
    {
        var frontendOrigin =
            builder.Configuration["FRONTEND_ORIGIN"]
            ?? builder.Configuration["Frontend:Origin"]
            ?? "http://localhost:5173";
        var frontendOrigins = frontendOrigin
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        policy
            .WithOrigins(frontendOrigins.Length == 0 ? ["http://localhost:5173"] : frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition", "X-Export-Row-Count");
    });
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DatabaseConnectionStringProvider>();
builder.Services.AddScoped<BusinessDatabaseScope>();
builder.Services.AddScoped<BusinessDatabase>();
builder.Services.AddSingleton<CheongjuDatabase>();
builder.Services.AddSingleton<OsanDatabase>();
builder.Services.AddSingleton<BusinessUnitWorkerRunner>();
builder.Services.AddSingleton<BusinessUnitDirectoryStore>();
builder.Services.AddSingleton<BusinessUnitAccessAdministrationStore>();
builder.Services.AddSingleton<BusinessUnitDatabaseBoundaryValidator>();
builder.Services.AddSingleton<BusinessUnitResolver>();
builder.Services.AddSingleton<BusinessUnitMembershipBackfillRunner>();
builder.Services.AddSingleton<DatabaseHealthChecker>();
builder.Services.AddSingleton<DatabaseMigrationCatalog>();
builder.Services.AddSingleton<MigrationLedgerInspector>();
builder.Services.AddSingleton<BusinessUnitDirectoryMigrationCatalog>();
builder.Services.AddSingleton<DatabaseRuntimePrivilegeManager>();
builder.Services.AddSingleton<DatabaseMigrationRunner>();
builder.Services.AddSingleton<DatabaseRoleBootstrapper>();
builder.Services.AddSingleton<ReviewSafeStatusService>();
builder.Services.AddSingleton<DevelopmentIdentitySeeder>();
builder.Services.AddScoped<UserProfilePhotoStore>();
builder.Services.AddScoped<HomeMetricsStore>();
builder.Services.AddScoped<G2OperationsStore>();
builder.Services.AddScoped<NoticeStore>();
builder.Services.AddScoped<DeploymentMaintenanceStore>();
builder.Services.AddScoped<OsanPolicyStore>();
builder.Services.AddScoped<IProjectDeletionGuard, ProjectDeletionGuard>();
builder.Services.AddSingleton<ProjectExcelParser>();
builder.Services.AddScoped<ProjectStore>();
builder.Services.AddSingleton<OsanProjectExcelParser>();
builder.Services.AddScoped<OsanProjectStore>();
OsanProgressPhotoValidator.ConfigureDecoderResourceLimits();
builder.Services.AddScoped<OsanProgressStore>();
builder.Services.AddScoped<OsanPhotoEditStore>();
builder.Services.AddScoped<OsanWorkRequestStore>();
builder.Services.AddSingleton<ExcelWorkbookBuilder>();
builder.Services.AddSingleton<ExcelExportConcurrencyGate>();
builder.Services.AddScoped<DataExportAuditStore>();
builder.Services.AddScoped<AuditStore>();
builder.Services.AddScoped<ExcelExportService>();
builder.Services.AddScoped<SelectedExcelExportService>();
builder.Services.AddSingleton<PanelInformationExcelParser>();
builder.Services.AddScoped<PanelInformationStore>();
builder.Services.AddScoped<QrScanUrlBuilder>();
builder.Services.AddScoped<PanelQrStore>();
builder.Services.AddScoped<PanelQrRenderer>();
builder.Services.AddScoped<PendingStore>();
builder.Services.AddScoped<PendingTypeStore>();
builder.Services.AddScoped<ProcurementExcelParser>();
builder.Services.AddScoped<ProcurementStore>();
builder.Services.AddScoped<InteriorBusbarStore>();
builder.Services.AddInteriorBusbarPublication(builder.Configuration);
builder.Services.AddInteriorBusbarEcount(builder.Configuration);
builder.Services.AddScoped<MaterialsStore>();
builder.Services.AddScoped<PanelKittingStore>();
builder.Services.AddScoped<ManufacturingStore>();
builder.Services.AddScoped<LogisticsStore>();
builder.Services.AddScoped<SalesSettlementStore>();
builder.Services.AddScoped<SalesBillingRequestStore>();
builder.Services.AddScoped<SalesKpiStore>();
builder.Services.AddScoped<Ul891SetStore>();
builder.Services.AddScoped<MonthlyBillingStore>();
builder.Services.AddScoped<IqcPdfRenderer>();
builder.Services.AddScoped<IqcReportStore>();
builder.Services.AddScoped<QualityInspectionPdfRenderer>();
builder.Services.AddScoped<QualityInspectionStore>();
builder.Services.AddScoped<ProductionPlanningStore>();
builder.Services.AddScoped<ProductionControlTemplateStore>();
builder.Services.AddScoped<SystemHolidayStore>();
builder.Services.AddScoped<BusinessCalendarStore>();
builder.Services.AddScoped<AdminCalendarHolidayStore>();
builder.Services.AddSingleton<CalendarHolidayExcelParser>();
builder.Services.AddScoped<AdminMasterDataStore>();
builder.Services.AddScoped<ApprovalPendingUserCountService>();
builder.Services.AddScoped<FormTemplateStore>();
builder.Services.AddScoped<MaterialCategoryStore>();
builder.Services.AddScoped<MaterialCategoryIqcStore>();
builder.Services.AddScoped<AdminScheduledDeletionService>();
builder.Services.AddScoped<IAdminDeletionPurgeService>(services =>
    services.GetRequiredService<AdminScheduledDeletionService>());
builder.Services.AddOptions<AdminDeletionPurgeOptions>()
    .Bind(builder.Configuration.GetSection(AdminDeletionPurgeOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<AdminDeletionPurgeOptions>, AdminDeletionPurgeOptionsValidator>();
builder.Services.AddScoped<WorkflowStore>();
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<NotificationOptions>, NotificationOptionsValidator>();
builder.Services.AddOptions<NotificationOptions>().ValidateOnStart();
builder.Services.AddSingleton<NotificationWorkerIdentity>();
builder.Services.AddScoped<NotificationDeliveryStore>();
builder.Services.AddScoped<NotificationPreferenceStore>();
builder.Services.AddScoped<OsanNotificationPreferenceStore>();
builder.Services.AddScoped<NotificationPreferenceAuditStore>();
builder.Services.AddScoped<WebPushSubscriptionStore>();
builder.Services.AddScoped<IWebPushSubscriptionDeliveryStore>(services =>
    services.GetRequiredService<WebPushSubscriptionStore>());
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<WorkItemEscalationStore>();
builder.Services.AddScoped<NotificationEscalationService>();
if (!reviewSafeEnabled)
{
    builder.Services.AddScoped<INotificationChannelHandler, TeamsChannelHandler>();
    builder.Services.AddScoped<INotificationChannelHandler, TeamsDirectMessageHandler>();
    builder.Services.AddScoped<INotificationChannelHandler, TeamsActivityChannelHandler>();
    builder.Services.AddScoped<INotificationChannelHandler, MailChannelHandler>();
    builder.Services.AddScoped<INotificationChannelHandler, WebPushChannelHandler>();
    builder.Services.AddSingleton<IWebPushProtocolClient, WebPushProtocolClient>();
    builder.Services.AddHttpClient<IGraphTokenProvider, GraphClientCredentialsTokenProvider>();
    builder.Services.AddScoped<IMailClient, ConfiguredMailClient>();
    builder.Services.AddScoped<ISmtpMailClient, SmtpMailClient>();
    builder.Services.AddScoped<ISmtpMailTransport, MailKitSmtpMailTransport>();
    builder.Services.AddHttpClient<IGraphMailClient, GraphMailClient>();
    builder.Services.AddHttpClient<ITeamsWebhookClient, TeamsWebhookClient>();
    builder.Services.AddHttpClient<ITeamsActivityClient, GraphTeamsActivityClient>();
}
if (mutationWorkerActivation.NotificationDeliveryWorkerEnabled)
{
    builder.Services.AddHostedService<NotificationDeliveryWorker>();
}
if (mutationWorkerActivation.NotificationEscalationWorkerEnabled)
{
    builder.Services.AddHostedService<NotificationEscalationWorker>();
}
if (mutationWorkerActivation.AdminDeletionPurgeWorkerEnabled)
{
    builder.Services.AddHostedService<AdminDeletionPurgeWorker>(services => new AdminDeletionPurgeWorker(
        null, services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AdminDeletionPurgeOptions>>(),
        services.GetRequiredService<ILogger<AdminDeletionPurgeWorker>>(),
        services.GetRequiredService<BusinessUnitWorkerRunner>()));
}
if (reviewSafeEnabled)
{
    builder.Services.AddSingleton<IKoreanHolidayProvider, ReviewSafeKoreanHolidayProvider>();
}
else
{
    builder.Services.AddHttpClient<IKoreanHolidayProvider, OfficialKoreanHolidayProvider>();
}
builder.Services.AddQmsAuthorizationFoundation(builder.Configuration, builder.Environment);

var app = builder.Build();

DevelopmentFeaturePolicy.ThrowIfInvalidActivation(
    DevelopmentFeaturePolicy.EvaluateDevelopmentAuthentication(app.Environment, app.Configuration),
    app.Environment);
DevelopmentFeaturePolicy.ThrowIfInvalidActivation(
    DevelopmentFeaturePolicy.EvaluateDevelopmentDataSeeding(app.Environment, app.Configuration),
    app.Environment);
DevelopmentFeaturePolicy.ThrowIfInvalidActivation(
    DevelopmentFeaturePolicy.EvaluateAdminUserSwitch(app.Environment, app.Configuration),
    app.Environment);
var maintenanceCommand = args.SingleOrDefault(DeploymentMaintenanceCli.Commands.Contains);
var migrateOnly = args.Contains("--migrate-only", StringComparer.Ordinal);
var deploymentDrainOnly = args.Contains("--deployment-drain-check", StringComparer.Ordinal);
var bootstrapDatabaseRolesOnly = args.Contains("--bootstrap-database-roles", StringComparer.Ordinal);
var backfillBusinessUnitMembershipsOnly = args.Contains("--backfill-business-unit-memberships", StringComparer.Ordinal);
var inspectBusinessUnitMembershipBackfillOnly = args.Contains(
    "--inspect-business-unit-membership-backfill",
    StringComparer.Ordinal);
var splitDatabaseRolesEnabled = !string.IsNullOrWhiteSpace(app.Configuration["Database:MigrationRoleName"])
    || !string.IsNullOrWhiteSpace(app.Configuration["Database:RuntimeRoleName"]);
if (new[]
    {
        migrateOnly,
        deploymentDrainOnly,
        bootstrapDatabaseRolesOnly,
        backfillBusinessUnitMembershipsOnly,
        inspectBusinessUnitMembershipBackfillOnly,
        maintenanceCommand is not null
    }.Count(selected => selected) > 1)
{
    app.Logger.LogError("Only one database operation mode can be selected.");
    Environment.ExitCode = 1;
    return;
}

if ((migrateOnly || deploymentDrainOnly) && (splitDatabaseRolesEnabled || businessUnitConfiguration.Enabled))
{
    try
    {
        DatabaseOperationSecurityPolicy.ThrowIfInvalid(
            app.Environment,
            app.Configuration,
            DatabaseOperationMode.Migration);
    }
    catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
    {
        app.Logger.LogError("Database operation failed. Code=database_operation_configuration_invalid.");
        Environment.ExitCode = 1;
        return;
    }
}
else if (bootstrapDatabaseRolesOnly)
{
    DatabaseOperationSecurityPolicy.ThrowIfInvalid(
        app.Environment,
        app.Configuration,
        DatabaseOperationMode.RoleBootstrap);
}
else if (backfillBusinessUnitMembershipsOnly || inspectBusinessUnitMembershipBackfillOnly)
{
    DatabaseOperationSecurityPolicy.ThrowIfInvalid(
        app.Environment,
        app.Configuration,
        DatabaseOperationMode.MembershipBackfill);
}
else
{
    if (businessUnitConfiguration.Enabled)
    {
        var databaseConfigurationErrors = businessUnitConfiguration
            .ValidateOperationConnections(
                app.Configuration,
                BusinessUnitConnectionPurpose.Runtime,
                requireSsl: app.Environment.IsProduction())
            .Concat(businessUnitConfiguration.ValidateSameServer(
                app.Configuration,
                BusinessUnitConnectionPurpose.Runtime))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (databaseConfigurationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Business-unit runtime database configuration is invalid ({databaseConfigurationErrors.Count} validation error(s)).");
        }
    }
    QmsAuthenticationModePolicy.ThrowIfInvalidConfiguration(app.Environment, app.Configuration);
    ProductionSecurityPolicy.ThrowIfInvalid(
        app.Environment,
        app.Configuration,
        requireRestoreVerification: !migrateOnly);
}

if (deploymentDrainOnly)
{
    // This mode never starts HTTP, seeders, workers, or database migrations.
    var target = app.Configuration["Database:MigrationTarget"] ?? string.Empty;
    var rawMaintenance = app.Configuration["DeploymentDrain:RequireMaintenance"] ?? "true";
    var validMaintenance = bool.TryParse(rawMaintenance, out var requireMaintenance);
    var validRelease = Guid.TryParse(app.Configuration["DeploymentDrain:ReleaseId"], out var releaseId)
        && releaseId != Guid.Empty;
    if (!validMaintenance || (requireMaintenance && !validRelease))
    {
        app.Logger.LogError("Deployment drain failed. Code=drain_configuration_invalid.");
        Environment.ExitCode = 1;
        return;
    }
    var checker = new DeploymentDrainChecker(app.Configuration,
        app.Services.GetRequiredService<DatabaseConnectionStringProvider>(),
        app.Services.GetRequiredService<ILogger<DeploymentDrainChecker>>());
    var result = await checker.CheckAsync(target, releaseId, requireMaintenance, CancellationToken.None);
    app.Logger.LogInformation("Deployment drain completed. Safe={IsSafe}; Code={Code}.", result.IsSafe, result.Code);
    Environment.ExitCode = result.IsSafe ? 0 : 1;
    return;
}

if (bootstrapDatabaseRolesOnly)
{
    var bootstrapper = app.Services.GetRequiredService<DatabaseRoleBootstrapper>();
    if (businessUnitConfiguration.Enabled)
        await bootstrapper.BootstrapAsync(app.Configuration["Database:BootstrapTarget"]
            ?? throw new InvalidOperationException("Database:BootstrapTarget is required for business-unit bootstrap."),
            CancellationToken.None);
    else
        await bootstrapper.BootstrapAsync(CancellationToken.None);
    app.Logger.LogInformation("Database role bootstrap completed.");
    return;
}

if (migrateOnly)
{
    var runner = app.Services.GetRequiredService<DatabaseMigrationRunner>();
    try
    {
        var inspection = businessUnitConfiguration.Enabled
            ? await runner.ApplyAndVerifyAsync(
                app.Configuration["Database:MigrationTarget"]
                    ?? throw new InvalidOperationException("Database:MigrationTarget is required for business-unit migrations."),
                CancellationToken.None)
            : await runner.ApplyAndVerifyAsync(CancellationToken.None);
        app.Logger.LogInformation(
            "Database migration completed with {ExpectedMigrationCount} verified migrations.",
            inspection.ExpectedMigrationCount);
    }
    catch (Npgsql.PostgresException exception)
    {
        // A rejected migration is a normal CLI failure, not an unhandled process
        // abort. Never print SQL, row details, or arbitrary server exception text.
        var failureCode = exception.SqlState == "P0001"
            && exception.MessageText == "business_schema_explicit_consent_required"
            ? "business_schema_explicit_consent_required"
            : "database_migration_failed";
        app.Logger.LogError("Database migration failed. Code={FailureCode}; SqlState={SqlState}.",
            failureCode, exception.SqlState);
        Environment.ExitCode = 1;
    }
    catch (Exception exception) when (exception is Npgsql.NpgsqlException or InvalidOperationException or ArgumentException)
    {
        app.Logger.LogError("Database migration failed. Code=database_migration_failed; ExceptionType={ExceptionType}.",
            exception.GetType().Name);
        Environment.ExitCode = 1;
    }
    return;
}

if (backfillBusinessUnitMembershipsOnly || inspectBusinessUnitMembershipBackfillOnly)
{
    var count = await app.Services
        .GetRequiredService<BusinessUnitMembershipBackfillRunner>()
        .ApplyAsync(
            CancellationToken.None,
            dryRun: inspectBusinessUnitMembershipBackfillOnly);
    app.Logger.LogInformation(
        "Business-unit membership backfill {Mode} completed with {IdentityCount} approved identities.",
        inspectBusinessUnitMembershipBackfillOnly ? "inspection" : "apply",
        count);
    return;
}

if (maintenanceCommand is not null)
{
    await DeploymentMaintenanceCli.RunAsync(maintenanceCommand, app.Configuration,
        app.Services.GetRequiredService<DatabaseConnectionStringProvider>(),
        app.Services.GetRequiredService<DatabaseHealthChecker>(), app.Logger, CancellationToken.None);
    return;
}

app.UseForwardedHeaders();
app.UseMiddleware<HostFilteringMiddleware>();
if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exception = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?
            .Error;
        if (exception is DepartmentHeadRequiredException departmentHeadRequired)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/problem+json";
            await Results.Problem(
                title: "부서장 지정이 필요합니다.",
                detail: departmentHeadRequired.Message,
                statusCode: StatusCodes.Status409Conflict)
                .ExecuteAsync(context);
            return;
        }
        if (exception is not null)
        {
            context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Emi.Qms.Api.UnhandledException")
                .LogError(exception, "Unhandled API exception for {Method} {Path}.", context.Request.Method, context.Request.Path);
        }
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await Results.Problem(
            title: "처리 중 오류가 발생했습니다.",
            detail: "잠시 후 다시 시도해 주세요.",
            statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(context);
    });
});

app.UseMiddleware<BusinessUnitRouteMiddleware>();
app.UseRouting();
app.UseCors("FrontendDevelopment");
app.UseMiddleware<ReviewSafeMutationGuardMiddleware>();
app.UseAuthentication();
app.UseMiddleware<BusinessUnitCapabilityMiddleware>();
if (builder.Configuration.GetValue("RateLimiting:Enabled", true))
{
    app.UseRateLimiter();
}
app.UseMiddleware<AdminUserSwitchGuardMiddleware>();
app.UseAuthorization();
app.UseMiddleware<DeploymentMaintenanceMiddleware>();
app.UseMiddleware<AuditMutationMiddleware>();
app.UseMiddleware<UploadSecurityMiddleware>();

if (!reviewSafeEnabled
    && !businessUnitConfiguration.Enabled
    && (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup")
    || builder.Configuration.GetValue<bool>("DATABASE_APPLY_MIGRATIONS_ON_STARTUP"))
   )
{
    await app.Services
        .GetRequiredService<DatabaseMigrationRunner>()
        .ApplyAsync(CancellationToken.None);
}

var developmentIdentitySeeder = app.Services.GetRequiredService<DevelopmentIdentitySeeder>();
if (developmentIdentitySeeder.IsEnabled())
{
    await developmentIdentitySeeder.SeedAsync(CancellationToken.None);
}

app.MapGet("/health/live", (TimeProvider timeProvider) =>
{
    return Results.Ok(new HealthResponse("live", "ok", timeProvider.GetUtcNow()));
})
.AllowAnonymous()
.WithName("LiveHealth");

app.MapGet("/health/ready", async (DatabaseHealthChecker databaseHealthChecker, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    if (reviewSafeEnabled)
    {
        var reviewStatus = await app.Services
            .GetRequiredService<ReviewSafeStatusService>()
            .CheckAsync(cancellationToken);
        var response = new ReadyHealthResponse(
            "ready",
            reviewStatus.Ready ? "ok" : "degraded",
            new DatabaseHealthResult(reviewStatus.Ready, reviewStatus.Ready ? "ready" : "not_ready"),
            timeProvider.GetUtcNow());

        return reviewStatus.Ready
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var database = await databaseHealthChecker.CheckAsync(cancellationToken);
    var status = database.IsReady && database.Reason == "reachable" ? "ok" : "degraded";

    var readyResponse = new ReadyHealthResponse(
        "ready",
        status,
        new DatabaseHealthResult(database.IsReady, database.IsReady ? "ready" : "not_ready", database.BusinessUnits),
        timeProvider.GetUtcNow());
    return database.IsReady
        ? Results.Ok(readyResponse)
        : Results.Json(readyResponse, statusCode: StatusCodes.Status503ServiceUnavailable);
})
.AllowAnonymous()
.WithName("ReadyHealth");

app.MapGet("/api/runtime-mode", async (ReviewSafeStatusService statusService, CancellationToken cancellationToken) =>
{
    return Results.Ok(await statusService.CheckAsync(cancellationToken));
})
.RequireAuthorization()
.WithName("RuntimeMode");

app.MapIdentityEndpoints();
app.MapBusinessUnitAccessEndpoints();
app.MapAuditEndpoints();
app.MapHomeMetricsEndpoints();
app.MapG2OperationsEndpoints();
app.MapNoticeEndpoints();
app.MapOsanNoticeEndpoints();
app.MapDeploymentMaintenanceEndpoints();
app.MapOsanPolicyEndpoints();
app.MapProjectEndpoints();
app.MapOsanProjectEndpoints();
app.MapOsanProgressEndpoints();
app.MapOsanManagementEndpoints();
app.MapPanelInformationEndpoints();
app.MapPanelQrEndpoints();
app.MapPendingEndpoints();
app.MapPendingTypeEndpoints();
app.MapProcurementEndpoints();
app.MapInteriorBusbarEndpoints();
app.MapMaterialsEndpoints();
app.MapPanelKittingEndpoints();
app.MapManufacturingEndpoints();
app.MapLogisticsEndpoints();
app.MapSalesSettlementEndpoints();
app.MapSalesBillingRequestEndpoints();
app.MapSalesKpiEndpoints();
app.MapUl891SetEndpoints();
app.MapQualityInspectionEndpoints();
app.MapProductionPlanningEndpoints();
app.MapProductionControlTemplateEndpoints();
app.MapBusinessCalendarEndpoints();
app.MapAdminCalendarHolidayEndpoints();
app.MapAdminMasterDataEndpoints();
app.MapFormTemplateEndpoints();
app.MapWorkflowEndpoints();
app.MapDataExportEndpoints();
app.MapNotificationDeliveryEndpoints();
app.MapNotificationEscalationEndpoints();
app.MapNotificationPreferenceEndpoints();
app.MapOsanNotificationPreferenceEndpoints();
app.MapNotificationPreferenceAuditEndpoints();
app.MapWebPushEndpoints();

AuditMutationRegistry.ValidateCoverage(((IEndpointRouteBuilder)app).DataSources);

app.Run();

public partial class Program
{
}
