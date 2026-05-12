using Banter.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Banter.Infrastructure;

internal static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Postgres"));
        });

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
            options.InstanceName = "Banter_";
        });

        services.AddHybridCache(options => 
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions()
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(2),
                Expiration = TimeSpan.FromMinutes(5) 
            };
        });

        return services;
    }
}
