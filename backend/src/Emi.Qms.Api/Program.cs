using Emi.Qms.Api;

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
builder.Services.AddSingleton<DatabaseHealthChecker>();

var app = builder.Build();

app.UseCors("FrontendDevelopment");

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

app.Run();

public partial class Program
{
}
