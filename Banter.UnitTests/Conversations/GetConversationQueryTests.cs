using Banter.Application.Bots;
using Banter.Application.Features.Conversations;
using Banter.Domain.Conversations;
using Banter.SharedKernel;
using Banter.UnitTests.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace Banter.UnitTests.Conversations;

public class GetConversationQueryTests
{
    [Fact]
    public async Task Handle_ReturnsConversationMetadataAndParticipantsWithPresence()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");
        var mohamed = database.AddUser("mohamed@example.com", "Mohamed");
        var ada = database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var conversation = Conversation.CreateGroupConversation(
            [ahmed.Id, sara.Id, mohamed.Id, ada.Id],
            "Backend Team");

        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var handler = new GetConversationQueryHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            new FakePresenceService(sara.Id));

        var result = await handler.Handle(new GetConversationQuery(conversation.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var response = result.Value;
        Assert.Equal(conversation.Id, response.ConversationId);
        Assert.Equal("Backend Team", response.Title);
        Assert.True(response.IsGroup);
        Assert.Equal(4, response.Participants.Count);

        Assert.True(response.Participants.Single(p => p.UserId == sara.Id).IsOnline);
        Assert.False(response.Participants.Single(p => p.UserId == ahmed.Id).IsOnline);
        Assert.False(response.Participants.Single(p => p.UserId == mohamed.Id).IsOnline);
        Assert.True(response.Participants.Single(p => p.UserId == ada.Id).IsBot);
        Assert.False(response.Participants.Single(p => p.UserId == sara.Id).IsBot);
    }

    [Fact]
    public async Task Handle_HidesBotConversationsWhenBotsAreDisabled()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var ada = database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, ada.Id);
        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var handler = new GetConversationQueryHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            new FakePresenceService(),
            Options.Create(new BotOptions { Enabled = false }));

        var result = await handler.Handle(new GetConversationQuery(conversation.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conversations.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_DeniesAccessToNonParticipants()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");
        var omar = database.AddUser("omar@example.com", "Omar");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, sara.Id);
        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var handler = new GetConversationQueryHandler(
            database.Context,
            new FakeUserContext(omar.Id),
            new FakePresenceService());

        var result = await handler.Handle(new GetConversationQuery(conversation.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conversations.AccessDenied", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundForAnUnknownConversation()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        await database.SaveAsync();

        var handler = new GetConversationQueryHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            new FakePresenceService());

        var result = await handler.Handle(new GetConversationQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conversations.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
