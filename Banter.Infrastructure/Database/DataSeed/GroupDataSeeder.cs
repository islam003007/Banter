using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.Infrastructure.Database.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banter.Infrastructure.Database.DataSeed;

/// <summary>
/// Seeds the group-conversation demo data idempotently. The normal seeders skip an entity type once
/// the table has any rows, so an already-seeded development database would otherwise never receive
/// these groups, their extra messages or Ahmed's Weekend Trip membership. Rows are inserted by id
/// (participants by conversation/user pair), so running this on every startup cannot duplicate data.
/// The conversation preview is resolved after the messages exist.
/// </summary>
internal static class GroupDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conversations = JsonDataLoader.LoadAsync<Conversation>("GroupConversations.json");

        foreach (var conversation in conversations)
        {
            if (!await dbContext.Conversations.AnyAsync(c => c.Id == conversation.Id))
                dbContext.Conversations.Add(conversation);
        }

        await dbContext.SaveChangesAsync();

        var messages = JsonDataLoader.LoadAsync<Message>("GroupMessages.json");

        foreach (var message in messages)
        {
            if (!await dbContext.Messages.AnyAsync(m => m.Id == message.Id))
                dbContext.Messages.Add(message);
        }

        await dbContext.SaveChangesAsync();

        var participants = JsonDataLoader.LoadAsync<ConversationParticipant>("GroupParticipants.json");

        foreach (var participant in participants)
        {
            var exists = await dbContext.ConversationParticipants.AnyAsync(p =>
                p.ConversationId == participant.ConversationId && p.UserId == participant.UserId);

            if (!exists)
                dbContext.ConversationParticipants.Add(participant);
        }

        await dbContext.SaveChangesAsync();

        // GroupMessages also appends to the original Backend Team and Weekend Trip threads.
        var touchedConversationIds = messages.Select(m => m.ConversationId).Distinct().ToList();

        var touchedConversations = await dbContext.Conversations
            .Where(c => touchedConversationIds.Contains(c.Id))
            .ToListAsync();

        foreach (var conversation in touchedConversations)
        {
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
