using Banter.Domain.Users;
using Banter.Infrastructure.Database.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Banter.Infrastructure.Database.DataSeed;

public static class IdentityDataSeeder
{
    public async static Task SeedUsers(IServiceProvider serviceProvider)
    {
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var password = scope.ServiceProvider.GetRequiredService<IConfiguration>()["DataSeed:UsersPassword"] ??
                throw new InvalidOperationException("DataSeed:UsersPassword is not configured");

            var users = JsonDataLoader.LoadAsync<User>("Users.json");

            foreach (var user in users)
            {
                var existingUser = await userManager.FindByEmailAsync(user.Email!);

                if (existingUser is not null)
                {
                    continue;
                }

                var result = await userManager.CreateAsync(user, password);
            }
        }
    }
}
