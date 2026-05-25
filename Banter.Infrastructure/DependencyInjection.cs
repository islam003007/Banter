using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Realtime;
using Banter.Infrastructure.Database;
using Banter.Infrastructure.Services;
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

        services.AddSingleton<IPresenceService, PresenseService>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<ISignalRNotifier, SignalRNotifier>();

        return services;
    }
}
