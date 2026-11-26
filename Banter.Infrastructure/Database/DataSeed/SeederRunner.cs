using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.Application.Bots;
using Banter.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Banter.Infrastructure.Database.DataSeed;

public static class SeederRunner
{
    public static async Task ApplyMigrations(IServiceProvider services)
    {
        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await dbContext.Database.MigrateAsync();
        }
    }

    /// <summary>
    /// Clears all demo data and re-runs the development seeders. This must only be used
    /// against a dedicated demo database; the caller enforces the demo configuration.
    /// </summary>
    public static async Task ResetDemoDataAsync(IServiceProvider serviceProvider)
    {
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Break the restrictive message references before deleting the message rows.
            await dbContext.Conversations.ExecuteUpdateAsync(setters =>
                setters.SetProperty(conversation => conversation.LastMessageId, (Guid?)null));
            await dbContext.ConversationParticipants.ExecuteUpdateAsync(setters =>
                setters.SetProperty(participant => participant.LastSeenMessageId, (Guid?)null));

            await dbContext.Messages.ExecuteDeleteAsync();
            await dbContext.ConversationParticipants.ExecuteDeleteAsync();
            await dbContext.Conversations.ExecuteDeleteAsync();

            await dbContext.UserRoles.ExecuteDeleteAsync();
            await dbContext.UserClaims.ExecuteDeleteAsync();
            await dbContext.UserLogins.ExecuteDeleteAsync();
            await dbContext.UserTokens.ExecuteDeleteAsync();
            await dbContext.RoleClaims.ExecuteDeleteAsync();
            await dbContext.Users.ExecuteDeleteAsync();
            await dbContext.Roles.ExecuteDeleteAsync();
        }

        await SeedDevelopment(serviceProvider);
    }

    public async static Task SeedDevelopment(IServiceProvider serviceProvider)
    {
        var botsEnabled = serviceProvider
            .GetRequiredService<IOptions<BotOptions>>()
            .Value
            .Enabled;

        await IdentityDataSeeder.SeedUsers(serviceProvider);

        if (botsEnabled)
        {
            await IdentityDataSeeder.SeedBots(serviceProvider);
        }

        await DataSeeder.SeedAsync<Conversation>(serviceProvider, "Conversations.json");
        await DataSeeder.SeedAsync<Message>(serviceProvider, "Messages.json");
        await DataSeeder.SeedAsync<ConversationParticipant>(serviceProvider, "ConversationParticipants.json");

        if (botsEnabled)
        {
            await BotDataSeeder.SeedAsync(serviceProvider);
        }

        await GroupDataSeeder.SeedAsync(serviceProvider);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetService<AppDbContext>()!;

            var conversations = await dbContext.Conversations.ToListAsync();

            foreach (var conversation in conversations) // N + 1 Problem. But this only for data seeding used for testing.
            {
                conversation.LastMessageId = await dbContext.Messages
                    .Where(m => m.ConversationId == conversation.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Id)
                    .FirstOrDefaultAsync();
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
