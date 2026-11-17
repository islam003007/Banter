using Banter.Application.Features.Conversations;
using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.UnitTests.TestSupport;
using Xunit;

namespace Banter.UnitTests.Conversations;

public class GetInboxQueryTests
{
    [Fact]
    public async Task Handle_IncludesConversationsWithoutMessagesAndCountsUnreadMessages()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");
        var mohamed = database.AddUser("mohamed@example.com", "Mohamed");

        await database.SaveAsync();

        var activeConversation = Conversation.CreateDM(ahmed.Id, sara.Id);
        var emptyConversation = Conversation.CreateDM(ahmed.Id, mohamed.Id);

        database.Context.Conversations.AddRange(activeConversation, emptyConversation);

        await database.SaveAsync();

        var first = new Message(activeConversation.Id, sara.Id, "first");
        var mine = new Message(activeConversation.Id, ahmed.Id, "mine");
        var latest = new Message(activeConversation.Id, sara.Id, "latest");

        database.Context.Messages.AddRange(first, mine, latest);

        await database.SaveAsync();

        activeConversation.LastMessageId = latest.Id;

        var ahmedParticipant = activeConversation.Participants.Single(p => p.UserId == ahmed.Id);
        ahmedParticipant.LastSeenMessageId = first.Id;

        await database.SaveAsync();

        var handler = new GetInboxHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            new FakePresenceService(sara.Id));

        var result = await handler.Handle(new GetInboxQuery(null, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var inbox = result.Value;
        Assert.Equal(2, inbox.Conversations.Count);
        Assert.False(inbox.HasMore);
        Assert.Null(inbox.Nextcursor);

        var active = inbox.Conversations.Single(c => c.ConversationId == activeConversation.Id);
        Assert.Equal("Sara", active.DisplayName);
        Assert.False(active.IsGroup);
        Assert.True(active.IsOnline);
        Assert.Equal("latest", active.LastMessageContent);
        Assert.NotNull(active.LastMessageCreatedAt);
        Assert.Equal(1, active.UnreadCount); // "mine" is excluded, "first" was seen.

        var empty = inbox.Conversations.Single(c => c.ConversationId == emptyConversation.Id);
        Assert.Equal("Mohamed", empty.DisplayName);
        Assert.Null(empty.LastMessageContent);
        Assert.Null(empty.LastMessageCreatedAt);
        Assert.Equal(0, empty.UnreadCount);
        Assert.False(empty.IsOnline);
    }

    [Fact]
    public async Task Handle_CountsAllMessagesAsUnreadWhenNothingWasSeenYet()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, sara.Id);
        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var first = new Message(conversation.Id, sara.Id, "first");
        var second = new Message(conversation.Id, sara.Id, "second");

        database.Context.Messages.AddRange(first, second);

        await database.SaveAsync();

        conversation.LastMessageId = second.Id;
        await database.SaveAsync();

        var handler = new GetInboxHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            new FakePresenceService());

        var result = await handler.Handle(new GetInboxQuery(null, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Conversations.Single().UnreadCount);
    }

    [Fact]
    public async Task Handle_PagesEmptyConversationsUsingTheirCreationTime()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");
        var mohamed = database.AddUser("mohamed@example.com", "Mohamed");

        await database.SaveAsync();

        var emptyConversation = Conversation.CreateDM(ahmed.Id, mohamed.Id);
        database.Context.Conversations.Add(emptyConversation);

        await database.SaveAsync();

        var activeConversation = Conversation.CreateDM(ahmed.Id, sara.Id);
        database.Context.Conversations.Add(activeConversation);

        await database.SaveAsync();

        var message = new Message(activeConversation.Id, sara.Id, "latest");
        database.Context.Messages.Add(message);

        await database.SaveAsync();

        activeConversation.LastMessageId = message.Id;
        await database.SaveAsync();

        var handler = new GetInboxHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            new FakePresenceService());

        var firstPage = await handler.Handle(new GetInboxQuery(null, 1), CancellationToken.None);

        Assert.True(firstPage.IsSuccess);
        Assert.True(firstPage.Value.HasMore);
        Assert.NotNull(firstPage.Value.Nextcursor);
        Assert.Equal(activeConversation.Id, firstPage.Value.Conversations.Single().ConversationId);

        var secondPage = await handler.Handle(
            new GetInboxQuery(firstPage.Value.Nextcursor, 1),
            CancellationToken.None);

        Assert.True(secondPage.IsSuccess);
        Assert.False(secondPage.Value.HasMore);
        Assert.Null(secondPage.Value.Nextcursor);
        Assert.Equal(emptyConversation.Id, secondPage.Value.Conversations.Single().ConversationId);
    }

    [Fact]
    public async Task Handle_ReportsNoUnreadMessagesAfterMarkingTheConversationAsRead()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, sara.Id);
        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var first = new Message(conversation.Id, sara.Id, "first");
        var second = new Message(conversation.Id, sara.Id, "second");

        database.Context.Messages.AddRange(first, second);

        await database.SaveAsync();

        conversation.LastMessageId = second.Id;
        await database.SaveAsync();

        var inboxHandler = new GetInboxHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            new FakePresenceService());

        var beforeMark = await inboxHandler.Handle(new GetInboxQuery(null, 20), CancellationToken.None);
        Assert.Equal(2, beforeMark.Value.Conversations.Single().UnreadCount);

        var markAsReadHandler = new MarkConversationAsReadCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var markResult = await markAsReadHandler.Handle(
            new MarkConversationAsReadCommand(conversation.Id),
            CancellationToken.None);

        Assert.True(markResult.IsSuccess);

        var afterMark = await inboxHandler.Handle(new GetInboxQuery(null, 20), CancellationToken.None);
        Assert.Equal(0, afterMark.Value.Conversations.Single().UnreadCount);

        var third = new Message(conversation.Id, sara.Id, "third");
        database.Context.Messages.Add(third);

        await database.SaveAsync();

        conversation.LastMessageId = third.Id;
        await database.SaveAsync();

        var afterNewMessage = await inboxHandler.Handle(new GetInboxQuery(null, 20), CancellationToken.None);
        Assert.Equal(1, afterNewMessage.Value.Conversations.Single().UnreadCount);
    }
}
