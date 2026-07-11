using Emi.Qms.Api;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Calendar;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.PanelInformation;
using Emi.Qms.Api.Procurement;
using Emi.Qms.Api.ProductionPlanning;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Workflow;

var builder = WebApplication.CreateBuilder(args);

ReviewSafeMode.ThrowIfInvalidActivation(builder.Environment, builder.Configuration);
var reviewSafeEnabled = ReviewSafeMode.IsEnabled(builder.Configuration);
var mutationWorkerActivation = MutationWorkerActivationPolicy.Evaluate(builder.Configuration, reviewSafeEnabled);

builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

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
            .WithExposedHeaders("Content-Disposition");
    });
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DatabaseConnectionStringProvider>();
builder.Services.AddSingleton<DatabaseHealthChecker>();
builder.Services.AddSingleton<DatabaseMigrationCatalog>();
builder.Services.AddSingleton<MigrationLedgerInspector>();
builder.Services.AddSingleton<DatabaseMigrationRunner>();
builder.Services.AddSingleton<ReviewSafeStatusService>();
builder.Services.AddSingleton<DevelopmentIdentitySeeder>();
builder.Services.AddSingleton<IProjectDeletionGuard, ProjectDeletionGuard>();
builder.Services.AddSingleton<ProjectExcelParser>();
builder.Services.AddSingleton<ProjectStore>();
builder.Services.AddSingleton<PanelInformationExcelParser>();
builder.Services.AddSingleton<PanelInformationStore>();
builder.Services.AddSingleton<ProcurementExcelParser>();
builder.Services.AddSingleton<ProcurementStore>();
builder.Services.AddSingleton<ProductionPlanningStore>();
builder.Services.AddSingleton<SystemHolidayStore>();
builder.Services.AddSingleton<BusinessCalendarStore>();
builder.Services.AddSingleton<AdminCalendarHolidayStore>();
builder.Services.AddSingleton<CalendarHolidayExcelParser>();
builder.Services.AddSingleton<AdminMasterDataStore>();
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
builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddSingleton<WorkItemEscalationStore>();
builder.Services.AddSingleton<NotificationEscalationService>();
if (!reviewSafeEnabled)
{
    builder.Services.AddSingleton<INotificationChannelHandler, TeamsChannelHandler>();
    builder.Services.AddSingleton<INotificationChannelHandler, TeamsDirectMessageHandler>();
    builder.Services.AddSingleton<INotificationChannelHandler, TeamsActivityChannelHandler>();
    builder.Services.AddSingleton<INotificationChannelHandler, MailChannelHandler>();
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
QmsAuthenticationModePolicy.ThrowIfInvalidConfiguration(app.Environment, app.Configuration);

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
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
app.UseMiddleware<AdminUserSwitchGuardMiddleware>();
app.UseAuthorization();

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
        var response = new ReviewSafeReadyHealthResponse(
            "ready",
            reviewStatus.Ready ? "ok" : "degraded",
            new DatabaseHealthResult(reviewStatus.Ready, reviewStatus.Reason),
            reviewStatus,
            timeProvider.GetUtcNow());

        return reviewStatus.Ready
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var database = await databaseHealthChecker.CheckAsync(cancellationToken);
    var status = database.IsReady ? "ok" : "degraded";

    return Results.Ok(new ReadyHealthResponse("ready", status, database, timeProvider.GetUtcNow()));
})
.AllowAnonymous()
.WithName("ReadyHealth");

app.MapGet("/api/runtime-mode", async (ReviewSafeStatusService statusService, CancellationToken cancellationToken) =>
{
    return Results.Ok(await statusService.CheckAsync(cancellationToken));
})
.AllowAnonymous()
.WithName("RuntimeMode");

app.MapIdentityEndpoints();
app.MapProjectEndpoints();
app.MapPanelInformationEndpoints();
app.MapProcurementEndpoints();
app.MapProductionPlanningEndpoints();
app.MapBusinessCalendarEndpoints();
app.MapAdminCalendarHolidayEndpoints();
app.MapAdminMasterDataEndpoints();
app.MapWorkflowEndpoints();
app.MapNotificationDeliveryEndpoints();
app.MapNotificationEscalationEndpoints();

app.Run();

public partial class Program
{
}
