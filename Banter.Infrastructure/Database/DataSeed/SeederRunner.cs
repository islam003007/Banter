using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    public async static Task SeedDevelopment(IServiceProvider serviceProvider)
    {
        await IdentityDataSeeder.SeedUsers(serviceProvider);
        await DataSeeder.SeedAsync<Conversation>(serviceProvider, "Conversations.json");
        await DataSeeder.SeedAsync<Message>(serviceProvider, "Messages.json");
        await DataSeeder.SeedAsync<ConversationParticipant>(serviceProvider, "ConversationParticipants.json");

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
