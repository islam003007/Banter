using Banter.Domain.Users;
using Banter.Infrastructure.Database.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Banter.Infrastructure.Database.DataSeed;

public static class IdentityDataSeeder
{
    public static Task SeedUsers(IServiceProvider serviceProvider) =>
        SeedUsersFrom(serviceProvider, "Users.json", assignPassword: true);

    /// <summary>
    /// Bot accounts are created without a password, so there is no credential that can be used to
    /// sign in even if the login guard were bypassed. They are seeded from a separate file so they
    /// can never leak into the demo-login allow-list.
    /// </summary>
    public static Task SeedBots(IServiceProvider serviceProvider) =>
        SeedUsersFrom(serviceProvider, "BotUsers.json", assignPassword: false);

    private static async Task SeedUsersFrom(IServiceProvider serviceProvider, string fileName, bool assignPassword)
    {
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            string? password = null;

            if (assignPassword)
            {
                password = scope.ServiceProvider.GetRequiredService<IConfiguration>()["DataSeed:UsersPassword"] ??
                    throw new InvalidOperationException("DataSeed:UsersPassword is not configured");
            }

            var users = JsonDataLoader.LoadAsync<User>(fileName);

            foreach (var user in users)
            {
                var existingUser = await userManager.FindByEmailAsync(user.Email!);

                if (existingUser is not null)
                {
                    continue;
                }

                var result = assignPassword
                    ? await userManager.CreateAsync(user, password!)
                    : await userManager.CreateAsync(user);

                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(error => error.Description));

                    throw new InvalidOperationException($"Failed to seed user {user.Email}: {errors}");
                }
            }
        }
    }
}
