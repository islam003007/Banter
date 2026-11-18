using Banter.Application.Abstractions.Bots;
using Banter.Application.Bots;
using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Banter.UnitTests.Bots;

public class BotReplyProcessorTests
{
    [Fact]
    public async Task Process_PersistsAndBroadcastsTheBotReply()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var ada = database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, ada.Id);

        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var humanMessage = new Message(conversation.Id, ahmed.Id, "How should I queue this?");

        database.Context.Messages.Add(humanMessage);
        conversation.LastMessageId = humanMessage.Id;

        await database.SaveAsync();

        var notifier = new FakeSignalRNotifier();
        var aiClient = new FakeAiChatClient("A bounded queue and an idempotency key.");

        var processor = new BotReplyProcessor(database.Context, notifier, aiClient);

        await processor.ProcessAsync(
            new BotReplyRequest(conversation.Id, ada.Id, ahmed.Id, humanMessage.Id));

        var typing = Assert.Single(notifier.TypingEvents);

        Assert.Equal(conversation.Id, typing.ConversationId);
        Assert.Equal("Ada", typing.BotName);
        Assert.Equal(new[] { ahmed.Id.ToString() }, typing.UserIds);

        Assert.NotNull(aiClient.Request);
        Assert.Equal("Ada", aiClient.Request.BotDisplayName);
        Assert.Equal("Ahmed", aiClient.Request.HumanDisplayName);
        Assert.Equal(humanMessage.Id, aiClient.Request.History.Last().Id);
        Assert.False(aiClient.Request.History.Last().IsBot);

        var botMessage = await database.Context.Messages.SingleAsync(m => m.UserId == ada.Id);

        Assert.Equal("A bounded queue and an idempotency key.", botMessage.Content);
        Assert.Equal(conversation.Id, botMessage.ConversationId);

        var savedConversation = await database.Context.Conversations
            .Include(c => c.Participants)
            .SingleAsync(c => c.Id == conversation.Id);

        Assert.Equal(botMessage.Id, savedConversation.LastMessageId);

        var botParticipant = savedConversation.Participants.Single(p => p.UserId == ada.Id);

        Assert.Equal(botMessage.Id, botParticipant.LastSeenMessageId);

        var notification = Assert.Single(notifier.Messages);

        Assert.Equal(botMessage.Id, notification.Message.Id);
        Assert.Equal(ada.Id, notification.Message.UserId);
        Assert.True(notification.Message.IsBot);
        Assert.Equal("Ada", notification.Message.SenderDisplayName);
        Assert.Equal(new[] { ahmed.Id.ToString() }, notification.UserIds);
    }

    [Fact]
    public async Task Process_IgnoresGroupConversations()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");
        var ada = database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var conversation = Conversation.CreateGroupConversation(
            [ahmed.Id, sara.Id, ada.Id],
            "Backend Team");

        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var humanMessage = new Message(conversation.Id, ahmed.Id, "Ada?");

        database.Context.Messages.Add(humanMessage);
        conversation.LastMessageId = humanMessage.Id;

        await database.SaveAsync();

        var notifier = new FakeSignalRNotifier();
        var processor = new BotReplyProcessor(database.Context, notifier, new FakeAiChatClient("nope"));

        await processor.ProcessAsync(
            new BotReplyRequest(conversation.Id, ada.Id, ahmed.Id, humanMessage.Id));

        Assert.Empty(notifier.TypingEvents);
        Assert.Empty(notifier.Messages);
        Assert.False(await database.Context.Messages.AnyAsync(m => m.UserId == ada.Id));
    }

    [Fact]
    public async Task Process_IgnoresAStaleTriggerMessage()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var ada = database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, ada.Id);

        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var firstMessage = new Message(conversation.Id, ahmed.Id, "First");
        var secondMessage = new Message(conversation.Id, ahmed.Id, "Second");

        database.Context.Messages.AddRange(firstMessage, secondMessage);
        conversation.LastMessageId = secondMessage.Id;

        await database.SaveAsync();

        var notifier = new FakeSignalRNotifier();
        var processor = new BotReplyProcessor(database.Context, notifier, new FakeAiChatClient("stale reply"));

        await processor.ProcessAsync(
            new BotReplyRequest(conversation.Id, ada.Id, ahmed.Id, firstMessage.Id));

        Assert.Empty(notifier.TypingEvents);
        Assert.Empty(notifier.Messages);
        Assert.False(await database.Context.Messages.AnyAsync(m => m.UserId == ada.Id));
    }

    [Fact]
    public async Task Process_DoesNotRunForBotToBotConversations()
    {
        await using var database = TestDatabase.Create();

        var ada = database.AddBot("ada@bots.local", "Ada");
        var milo = database.AddBot("milo@bots.local", "Milo");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ada.Id, milo.Id);

        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var message = new Message(conversation.Id, ada.Id, "Hello fellow bot");

        database.Context.Messages.Add(message);
        conversation.LastMessageId = message.Id;

        await database.SaveAsync();

        var notifier = new FakeSignalRNotifier();
        var processor = new BotReplyProcessor(database.Context, notifier, new FakeAiChatClient("loop"));

        await processor.ProcessAsync(
            new BotReplyRequest(conversation.Id, milo.Id, ada.Id, message.Id));

        Assert.Empty(notifier.TypingEvents);
        Assert.Empty(notifier.Messages);
    }
}
