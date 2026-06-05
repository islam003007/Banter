using Banter.API;
using Banter.API.Extensions;
using Banter.Application;
using Banter.Infrastructure;
using Banter.Infrastructure.Database.DataSeed;
using Banter.Infrastructure.Services.Realtime;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);


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

app.MapHub<ChatHub>("/hubs/chat");

app.Run();


