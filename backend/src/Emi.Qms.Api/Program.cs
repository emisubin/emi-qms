using Emi.Qms.Api;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Calendar;
using Emi.Qms.Api.DataExports;
using Emi.Qms.Api.Home;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Logistics;
using Emi.Qms.Api.Manufacturing;
using Emi.Qms.Api.Materials;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.Notices;
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
        Math.Max(1024, uploadSecurityConfiguration.MaximumFileBytes + (2 * 1024 * 1024));
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        Math.Max(1024, uploadSecurityConfiguration.MaximumFileBytes + (2 * 1024 * 1024));
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
builder.Services.AddSingleton<DatabaseHealthChecker>();
builder.Services.AddSingleton<DatabaseMigrationCatalog>();
builder.Services.AddSingleton<MigrationLedgerInspector>();
builder.Services.AddSingleton<DatabaseRuntimePrivilegeManager>();
builder.Services.AddSingleton<DatabaseMigrationRunner>();
builder.Services.AddSingleton<DatabaseRoleBootstrapper>();
builder.Services.AddSingleton<ReviewSafeStatusService>();
builder.Services.AddSingleton<DevelopmentIdentitySeeder>();
builder.Services.AddSingleton<UserProfilePhotoStore>();
builder.Services.AddSingleton<HomeMetricsStore>();
builder.Services.AddSingleton<NoticeStore>();
builder.Services.AddSingleton<IProjectDeletionGuard, ProjectDeletionGuard>();
builder.Services.AddSingleton<ProjectExcelParser>();
builder.Services.AddSingleton<ProjectStore>();
builder.Services.AddSingleton<ExcelWorkbookBuilder>();
builder.Services.AddSingleton<ExcelExportConcurrencyGate>();
builder.Services.AddSingleton<DataExportAuditStore>();
builder.Services.AddSingleton<ExcelExportService>();
builder.Services.AddSingleton<SelectedExcelExportService>();
builder.Services.AddSingleton<PanelInformationExcelParser>();
builder.Services.AddSingleton<PanelInformationStore>();
builder.Services.AddSingleton<PanelQrStore>();
builder.Services.AddSingleton<PanelQrRenderer>();
builder.Services.AddSingleton<PendingStore>();
builder.Services.AddSingleton<PendingTypeStore>();
builder.Services.AddSingleton<ProcurementExcelParser>();
builder.Services.AddSingleton<ProcurementStore>();
builder.Services.AddSingleton<MaterialsStore>();
builder.Services.AddSingleton<PanelKittingStore>();
builder.Services.AddSingleton<ManufacturingStore>();
builder.Services.AddSingleton<LogisticsStore>();
builder.Services.AddSingleton<SalesSettlementStore>();
builder.Services.AddSingleton<SalesBillingRequestStore>();
builder.Services.AddSingleton<SalesKpiStore>();
builder.Services.AddSingleton<Ul891SetStore>();
builder.Services.AddSingleton<MonthlyBillingStore>();
builder.Services.AddSingleton<IqcPdfRenderer>();
builder.Services.AddSingleton<IqcReportStore>();
builder.Services.AddSingleton<QualityInspectionPdfRenderer>();
builder.Services.AddSingleton<QualityInspectionStore>();
builder.Services.AddSingleton<ProductionPlanningStore>();
builder.Services.AddSingleton<ProductionControlTemplateStore>();
builder.Services.AddSingleton<SystemHolidayStore>();
builder.Services.AddSingleton<BusinessCalendarStore>();
builder.Services.AddSingleton<AdminCalendarHolidayStore>();
builder.Services.AddSingleton<CalendarHolidayExcelParser>();
builder.Services.AddSingleton<AdminMasterDataStore>();
builder.Services.AddSingleton<FormTemplateStore>();
builder.Services.AddSingleton<MaterialCategoryStore>();
builder.Services.AddSingleton<MaterialCategoryIqcStore>();
builder.Services.AddSingleton<AdminScheduledDeletionService>();
builder.Services.AddSingleton<IAdminDeletionPurgeService>(services =>
    services.GetRequiredService<AdminScheduledDeletionService>());
builder.Services.AddOptions<AdminDeletionPurgeOptions>()
    .Bind(builder.Configuration.GetSection(AdminDeletionPurgeOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<AdminDeletionPurgeOptions>, AdminDeletionPurgeOptionsValidator>();
builder.Services.AddSingleton<WorkflowStore>();
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<NotificationOptions>, NotificationOptionsValidator>();
builder.Services.AddOptions<NotificationOptions>().ValidateOnStart();
builder.Services.AddSingleton<NotificationWorkerIdentity>();
builder.Services.AddSingleton<NotificationDeliveryStore>();
builder.Services.AddSingleton<NotificationPreferenceStore>();
builder.Services.AddSingleton<NotificationPreferenceAuditStore>();
builder.Services.AddSingleton<WebPushSubscriptionStore>();
builder.Services.AddSingleton<IWebPushSubscriptionDeliveryStore>(services =>
    services.GetRequiredService<WebPushSubscriptionStore>());
builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddSingleton<WorkItemEscalationStore>();
builder.Services.AddSingleton<NotificationEscalationService>();
if (!reviewSafeEnabled)
{
    builder.Services.AddSingleton<INotificationChannelHandler, TeamsChannelHandler>();
    builder.Services.AddSingleton<INotificationChannelHandler, TeamsDirectMessageHandler>();
    builder.Services.AddSingleton<INotificationChannelHandler, TeamsActivityChannelHandler>();
    builder.Services.AddSingleton<INotificationChannelHandler, MailChannelHandler>();
    builder.Services.AddSingleton<INotificationChannelHandler, WebPushChannelHandler>();
    builder.Services.AddSingleton<IWebPushProtocolClient, WebPushProtocolClient>();
    builder.Services.AddHttpClient<IGraphTokenProvider, GraphClientCredentialsTokenProvider>();
    builder.Services.AddSingleton<IMailClient, ConfiguredMailClient>();
    builder.Services.AddSingleton<ISmtpMailClient, SmtpMailClient>();
    builder.Services.AddSingleton<ISmtpMailTransport, MailKitSmtpMailTransport>();
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
    builder.Services.AddHostedService<AdminDeletionPurgeWorker>();
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
var migrateOnly = args.Contains("--migrate-only", StringComparer.Ordinal);
var bootstrapDatabaseRolesOnly = args.Contains("--bootstrap-database-roles", StringComparer.Ordinal);
var splitDatabaseRolesEnabled = !string.IsNullOrWhiteSpace(app.Configuration["Database:MigrationRoleName"])
    || !string.IsNullOrWhiteSpace(app.Configuration["Database:RuntimeRoleName"]);
if (migrateOnly && bootstrapDatabaseRolesOnly)
{
    throw new InvalidOperationException("Only one database operation mode can be selected.");
}

if (migrateOnly && splitDatabaseRolesEnabled)
{
    DatabaseOperationSecurityPolicy.ThrowIfInvalid(
        app.Environment,
        app.Configuration,
        DatabaseOperationMode.Migration);
}
else if (bootstrapDatabaseRolesOnly)
{
    DatabaseOperationSecurityPolicy.ThrowIfInvalid(
        app.Environment,
        app.Configuration,
        DatabaseOperationMode.RoleBootstrap);
}
else
{
    QmsAuthenticationModePolicy.ThrowIfInvalidConfiguration(app.Environment, app.Configuration);
    ProductionSecurityPolicy.ThrowIfInvalid(
        app.Environment,
        app.Configuration,
        requireRestoreVerification: !migrateOnly);
}

if (bootstrapDatabaseRolesOnly)
{
    await app.Services
        .GetRequiredService<DatabaseRoleBootstrapper>()
        .BootstrapAsync(CancellationToken.None);
    app.Logger.LogInformation("Database role bootstrap completed.");
    return;
}

if (migrateOnly)
{
    var inspection = await app.Services
        .GetRequiredService<DatabaseMigrationRunner>()
        .ApplyAndVerifyAsync(CancellationToken.None);
    app.Logger.LogInformation(
        "Database migration completed with {ExpectedMigrationCount} verified migrations.",
        inspection.ExpectedMigrationCount);
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

app.UseCors("FrontendDevelopment");
app.UseMiddleware<ReviewSafeMutationGuardMiddleware>();
app.UseAuthentication();
if (builder.Configuration.GetValue("RateLimiting:Enabled", true))
{
    app.UseRateLimiter();
}
app.UseMiddleware<AdminUserSwitchGuardMiddleware>();
app.UseAuthorization();
app.UseMiddleware<UploadSecurityMiddleware>();

if (!reviewSafeEnabled
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
    var status = database.IsReady ? "ok" : "degraded";

    return Results.Ok(new ReadyHealthResponse(
        "ready",
        status,
        new DatabaseHealthResult(database.IsReady, database.IsReady ? "ready" : "not_ready"),
        timeProvider.GetUtcNow()));
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
app.MapHomeMetricsEndpoints();
app.MapNoticeEndpoints();
app.MapProjectEndpoints();
app.MapPanelInformationEndpoints();
app.MapPanelQrEndpoints();
app.MapPendingEndpoints();
app.MapPendingTypeEndpoints();
app.MapProcurementEndpoints();
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
app.MapNotificationPreferenceAuditEndpoints();
app.MapWebPushEndpoints();

app.Run();

public partial class Program
{
}
