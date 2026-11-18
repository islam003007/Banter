using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.Infrastructure.Database.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banter.Infrastructure.Database.DataSeed;

/// <summary>
/// Seeds the bot inbox demo data idempotently. The normal seeders skip an entity type once the table
/// has any rows, which would leave an existing development database without the Ada conversation.
/// These rows are inserted by id instead, so the demo always has one bot DM to open.
/// </summary>
internal static class BotDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conversations = JsonDataLoader.LoadAsync<Conversation>("BotConversations.json");

        foreach (var conversation in conversations)
        {
            if (!await dbContext.Conversations.AnyAsync(c => c.Id == conversation.Id))
                dbContext.Conversations.Add(conversation);
        }

        await dbContext.SaveChangesAsync();

        var messages = JsonDataLoader.LoadAsync<Message>("BotMessages.json");

        foreach (var message in messages)
        {
            if (!await dbContext.Messages.AnyAsync(m => m.Id == message.Id))
                dbContext.Messages.Add(message);
        }

        await dbContext.SaveChangesAsync();

        var participants = JsonDataLoader.LoadAsync<ConversationParticipant>("BotParticipants.json");

        foreach (var participant in participants)
        {
            var exists = await dbContext.ConversationParticipants.AnyAsync(p =>
                p.ConversationId == participant.ConversationId && p.UserId == participant.UserId);

            if (!exists)
                dbContext.ConversationParticipants.Add(participant);
        }

        await dbContext.SaveChangesAsync();

        foreach (var seedConversation in conversations)
        {
            var conversation = await dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == seedConversation.Id);

            if (conversation is null)
                continue;

            var lastMessageId = await dbContext.Messages
                .Where(m => m.ConversationId == conversation.Id)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(m => m.Id)
                .FirstOrDefaultAsync();

            if (lastMessageId != Guid.Empty)
                conversation.LastMessageId = lastMessageId;
        }

        await dbContext.SaveChangesAsync();
    }
}
