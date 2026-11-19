using Banter.API;
using Banter.API.Extensions;
using Banter.Application;
using Banter.Infrastructure;
using Banter.Infrastructure.Database;
using Banter.Infrastructure.Database.DataSeed;
using Banter.Infrastructure.Services.Realtime;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration, builder.Environment);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(type => type.FullName!.Replace("+", "."));
});

builder.Host.UseSerilog((context, services, logger) =>
{
    logger.ReadFrom.Configuration(context.Configuration);
});

var app = builder.Build();

await SeederRunner.ApplyMigrations(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "swagger";
    });

    await SeederRunner.SeedDevelopment(app.Services);
}

app.UseCustomRequestLogging();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();

// Liveness is dependency-free (orchestrators use it to decide whether to restart a process).
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithTags("health")
    .AllowAnonymous();

// Readiness proves this instance can actually serve traffic. It is intentionally small and
// dependency-specific rather than pulling in an extra health-check package.
app.MapGet("/health/ready", async (
    AppDbContext dbContext,
    IConnectionMultiplexer redis,
    CancellationToken cancellationToken) =>
{
    var databaseHealthy = await dbContext.Database.CanConnectAsync(cancellationToken);

    bool redisHealthy;

    try
    {
        await redis.GetDatabase().PingAsync();
        redisHealthy = true;
    }
    catch
    {
        redisHealthy = false;
    }

    var healthy = databaseHealthy && redisHealthy;

    return Results.Json(
        new
        {
            status = healthy ? "Healthy" : "Unhealthy",
            checks = new
            {
                database = databaseHealthy ? "Healthy" : "Unhealthy",
                redis = redisHealthy ? "Healthy" : "Unhealthy"
            }
        },
        statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
})
    .WithTags("health")
    .AllowAnonymous();

app.MapHub<ChatHub>("/hubs/chat");

app.Run();
