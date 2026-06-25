using Emi.Qms.Api;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDevelopment", policy =>
    {
        var frontendOrigin =
            builder.Configuration["Frontend:Origin"]
            ?? builder.Configuration["FRONTEND_ORIGIN"]
            ?? "http://localhost:5173";

        policy
            .WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DatabaseConnectionStringProvider>();
builder.Services.AddSingleton<DatabaseHealthChecker>();
builder.Services.AddSingleton<DatabaseMigrationRunner>();
builder.Services.AddSingleton<DevelopmentIdentitySeeder>();
builder.Services.AddSingleton<ProjectStore>();
builder.Services.AddQmsAuthorizationFoundation();

var app = builder.Build();

DevelopmentFeaturePolicy.ThrowIfInvalidActivation(
    DevelopmentFeaturePolicy.EvaluateDevelopmentAuthentication(app.Environment, app.Configuration),
    app.Environment);
DevelopmentFeaturePolicy.ThrowIfInvalidActivation(
    DevelopmentFeaturePolicy.EvaluateDevelopmentDataSeeding(app.Environment, app.Configuration),
    app.Environment);

app.UseCors("FrontendDevelopment");
app.UseAuthentication();
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

app.Run();

public partial class Program
{
}
