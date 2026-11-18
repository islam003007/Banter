using Banter.Application.Features.Messages;
using Banter.Domain.Conversations;
using Banter.UnitTests.TestSupport;
using Xunit;

namespace Banter.UnitTests.Messages;

public class SendMessageCommandTests
{
    [Fact]
    public async Task Handle_PublishesTheSavedMessageWithItsContent()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, sara.Id);
        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var notifier = new FakeSignalRNotifier();
        var queue = new CapturingBotReplyQueue();

        var handler = new SendMessageCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            notifier,
            queue);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "Hello Sara"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var captured = Assert.Single(notifier.Messages);
        var notification = captured.Message;

        Assert.Equal(result.Value, notification.Id);
        Assert.Equal(conversation.Id, notification.ConversationId);
        Assert.Equal(ahmed.Id, notification.UserId);
        Assert.Equal("Hello Sara", notification.Content);
        Assert.Equal("Ahmed", notification.SenderDisplayName);
        Assert.False(notification.IsBot);
        Assert.Equal(new[] { sara.Id.ToString() }, captured.UserIds);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Handle_HumanToBotDm_EnqueuesExactlyOneReply()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var ada = database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, ada.Id);
        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var notifier = new FakeSignalRNotifier();
        var queue = new CapturingBotReplyQueue();

        var handler = new SendMessageCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            notifier,
            queue);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "Ada, how should I queue this?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var request = Assert.Single(queue.Requests);

        Assert.Equal(conversation.Id, request.ConversationId);
        Assert.Equal(ada.Id, request.BotUserId);
        Assert.Equal(ahmed.Id, request.HumanUserId);
        Assert.Equal(result.Value, request.TriggeringMessageId);
    }

    [Fact]
    public async Task Handle_DoesNotEnqueueInGroupConversations()
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

        var queue = new CapturingBotReplyQueue();

        var handler = new SendMessageCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            new FakeSignalRNotifier(),
            queue);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "Ada, are you there?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Handle_BotMessage_NeverSchedulesAnotherReply()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var ada = database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, ada.Id);
        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var notifier = new FakeSignalRNotifier();
        var queue = new CapturingBotReplyQueue();

        var handler = new SendMessageCommandHandler(
            database.Context,
            new FakeUserContext(ada.Id),
            notifier,
            queue);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "Bot speaking."),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var notification = Assert.Single(notifier.Messages).Message;

        Assert.True(notification.IsBot);
        Assert.Empty(queue.Requests);
    }
}
