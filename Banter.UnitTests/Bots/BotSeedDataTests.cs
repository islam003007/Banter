using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.Domain.Users;
using Banter.Infrastructure.Database;
using Banter.Infrastructure.Database.DataSeed;
using Banter.Infrastructure.Database.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Banter.UnitTests.Bots;

public class BotSeedDataTests
{
    [Fact]
    public void BotUsers_HaveStableIdsAndTheBotFlag()
    {
        var bots = JsonDataLoader.LoadAsync<User>("BotUsers.json");

        Assert.Equal(3, bots.Count);
        Assert.All(bots, bot => Assert.True(bot.IsBot));
        Assert.All(bots, bot => Assert.Null(bot.ProfilePictureUrl));
        Assert.Equal(
            ["ada@bots.local", "milo@bots.local", "nova@bots.local"],
            bots.Select(bot => bot.Email));
        Assert.Equal(3, bots.Select(bot => bot.Id).Distinct().Count());
    }

    [Fact]
    public void BotConversation_IsTheSeededAdaDirectMessage()
    {
        var bots = JsonDataLoader.LoadAsync<User>("BotUsers.json");
        var conversations = JsonDataLoader.LoadAsync<Conversation>("BotConversations.json");
        var participants = JsonDataLoader.LoadAsync<ConversationParticipant>("BotParticipants.json");
        var messages = JsonDataLoader.LoadAsync<Message>("BotMessages.json");

        var conversation = Assert.Single(conversations);

        Assert.False(conversation.IsGroup);

        var conversationParticipants = participants
            .Where(participant => participant.ConversationId == conversation.Id)
            .ToList();

        Assert.Equal(2, conversationParticipants.Count);

        var ada = bots.Single(bot => bot.DisplayName == "Ada");

        Assert.Contains(conversationParticipants, participant => participant.UserId == ada.Id);
        Assert.Contains(
            conversationParticipants,
            participant => participant.UserId == Guid.Parse("019e8b9d-c84a-75a0-b965-7d25b890ad80"));

        var conversationMessages = messages
            .Where(message => message.ConversationId == conversation.Id)
            .OrderBy(message => message.CreatedAt)
            .ToList();

        Assert.Equal(2, conversationMessages.Count);
        Assert.Contains(conversationMessages, message => message.UserId == ada.Id);
    }

    [Fact]
    public async Task BotDataSeeder_IsIdempotent()
    {
        var services = new ServiceCollection();
        var databaseName = $"BotSeed-{Guid.NewGuid()}";

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        await using var provider = services.BuildServiceProvider();

        await BotDataSeeder.SeedAsync(provider);
        await BotDataSeeder.SeedAsync(provider);

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(1, await dbContext.Conversations.CountAsync());
        Assert.Equal(2, await dbContext.Messages.CountAsync());
        Assert.Equal(2, await dbContext.ConversationParticipants.CountAsync());
        Assert.NotNull(await dbContext.Conversations.Select(c => c.LastMessageId).SingleAsync());
    }
}
