using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Ai;
using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Bots;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Demo;
using Banter.Application.Abstractions.Realtime;
using Banter.Application.Bots;
using Banter.Application.Features.Auth;
using Banter.Infrastructure.Database;
using Banter.Infrastructure.Services;
using Banter.Infrastructure.Services.Ai;
using Banter.Infrastructure.Services.Bots;
using Banter.Infrastructure.Services.Demo;
using Banter.Infrastructure.Services.Realtime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Banter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Postgres"));
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        var redisConnection = configuration.GetConnectionString("Redis") ??
            throw new InvalidOperationException("ConnectionString:Redis is not configured.");

        IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnection);

        services.AddSingleton(redis); // for safe clean up.

        services.AddStackExchangeRedisCache(options =>
        {
            options.ConnectionMultiplexerFactory = () => Task.FromResult(redis);
        });

        services.AddDataProtection()
            .PersistKeysToStackExchangeRedis(redis, "Banter-DataProtection-Keys");

        services.AddSignalR().AddStackExchangeRedis(redisConnection, // no safe way to reuse multiplexer.
        options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("Banter");
        });

        services.AddSingleton<IPresenceService, PresenceService>();
        services.AddSingleton<IDemoUserDirectory, SeededDemoUserDirectory>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<ISignalRNotifier, SignalRNotifier>();

        services.Configure<BotOptions>(configuration.GetSection(BotOptions.SectionName));

        if (configuration.GetValue<bool>($"{BotOptions.SectionName}:Enabled"))
        {
            services.AddSingleton<IAiChatClient, DemoAiChatClient>();
            services.AddSingleton<IBotReplyQueue, BotReplyQueue>();
            services.AddHostedService<BotReplyBackgroundService>();
        }
        else
        {
            services.AddSingleton<IBotReplyQueue, NoOpBotReplyQueue>();
        }

        var demoResetSection = configuration.GetSection(DemoResetOptions.SectionName);
        if (configuration.GetValue<bool>("Demo:Enabled") &&
            demoResetSection.GetValue<bool>(nameof(DemoResetOptions.Enabled)))
        {
            services.AddOptions<DemoResetOptions>()
                .Bind(demoResetSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHostedService<DemoResetService>();
        }

        return services;
    }
}
