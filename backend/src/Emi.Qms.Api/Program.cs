using Emi.Qms.Api;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Calendar;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.PanelInformation;
using Emi.Qms.Api.Procurement;
using Emi.Qms.Api.ProductionPlanning;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.Workflow;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<DatabaseMigrationRunner>();
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
builder.Services.AddSingleton<WorkflowStore>();
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.AddSingleton<NotificationDeliveryStore>();
builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddSingleton<WorkItemEscalationStore>();
builder.Services.AddSingleton<NotificationEscalationService>();
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
builder.Services.AddHostedService<NotificationDeliveryWorker>();
builder.Services.AddHostedService<NotificationEscalationWorker>();
builder.Services.AddHttpClient<IKoreanHolidayProvider, OfficialKoreanHolidayProvider>();
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
app.UseAuthentication();
app.UseMiddleware<AdminUserSwitchGuardMiddleware>();
app.UseAuthorization();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup")
    || builder.Configuration.GetValue<bool>("DATABASE_APPLY_MIGRATIONS_ON_STARTUP"))
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
    var database = await databaseHealthChecker.CheckAsync(cancellationToken);
    var status = database.IsReady ? "ok" : "degraded";

    return Results.Ok(new ReadyHealthResponse("ready", status, database, timeProvider.GetUtcNow()));
})
.AllowAnonymous()
.WithName("ReadyHealth");

app.MapIdentityEndpoints();
app.MapProjectEndpoints();
app.MapPanelInformationEndpoints();
app.MapProcurementEndpoints();
app.MapProductionPlanningEndpoints();
app.MapBusinessCalendarEndpoints();
app.MapAdminCalendarHolidayEndpoints();
app.MapWorkflowEndpoints();
app.MapNotificationDeliveryEndpoints();
app.MapNotificationEscalationEndpoints();

app.Run();

public partial class Program
{
}
