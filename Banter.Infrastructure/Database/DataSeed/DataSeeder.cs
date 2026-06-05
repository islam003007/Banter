using Banter.Infrastructure.Database.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banter.Infrastructure.Database.DataSeed;

internal class DataSeeder
{
    public static async Task SeedAsync<TDomainType>(IServiceProvider serviceProvider, string fileName)
        where TDomainType : class
    {
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (await context.Set<TDomainType>().AnyAsync())
                return; // already seeded

            var seedData = JsonDataLoader.LoadAsync<TDomainType>(fileName);

            context.Set<TDomainType>().AddRange(seedData);
            await context.SaveChangesAsync();
        }
    }
}
