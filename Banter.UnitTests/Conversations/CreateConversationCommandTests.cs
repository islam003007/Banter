using Banter.Application.Features.Conversations;
using Banter.Application.Bots;
using Banter.Domain.Constants;
using Banter.Domain.Conversations;
using Banter.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Banter.UnitTests.Conversations;

public class CreateConversationCommandTests
{
    [Fact]
    public async Task Handle_CreatesADirectConversationForOneParticipant()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");

        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var result = await handler.Handle(
            new CreateConversationCommand([sara.Id]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var conversation = await database.Context.Conversations
            .Include(c => c.Participants)
            .SingleAsync(c => c.Id == result.Value);

        Assert.False(conversation.IsGroup);
        Assert.Null(conversation.Title);
        Assert.Equal(2, conversation.Participants.Count);
        Assert.Contains(conversation.Participants, p => p.UserId == ahmed.Id);
        Assert.Contains(conversation.Participants, p => p.UserId == sara.Id);
    }

    [Fact]
    public async Task Handle_ReusesAnExistingDirectConversation()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");

        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var first = await handler.Handle(new CreateConversationCommand([sara.Id]), CancellationToken.None);
        var second = await handler.Handle(new CreateConversationCommand([sara.Id]), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(1, await database.Context.Conversations.CountAsync());
    }

    [Fact]
    public async Task Handle_DoesNotReuseATwoPersonGroupAsADirectConversation()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var mohamed = database.AddUser("mohamed@example.com", "Mohamed");

        await database.SaveAsync();

        // A group can legitimately shrink to two participants; that must never be mistaken for
        // the unique direct thread between those two users.
        var group = Conversation.CreateGroupConversation([ahmed.Id, mohamed.Id], "Two Person Group");
        database.Context.Conversations.Add(group);
        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var result = await handler.Handle(
            new CreateConversationCommand([mohamed.Id]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(group.Id, result.Value);

        var direct = await database.Context.Conversations
            .Include(c => c.Participants)
            .SingleAsync(c => c.Id == result.Value);

        Assert.False(direct.IsGroup);
        Assert.Equal(2, direct.Participants.Count);
        Assert.Equal(2, await database.Context.Conversations.CountAsync());
    }

    [Fact]
    public async Task Handle_RejectsTheCreatorAsAParticipant()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");

        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var result = await handler.Handle(
            new CreateConversationCommand([ahmed.Id, sara.Id]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConversationErrors.CreatorCannotBeParticipant.Code, result.Error.Code);
        Assert.Empty(database.Context.Conversations);
    }

    [Fact]
    public async Task Handle_ReturnsACleanNotFoundWhenAParticipantDoesNotExist()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var missing = Guid.NewGuid();

        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var result = await handler.Handle(
            new CreateConversationCommand([missing]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conversations.ParticipantsNotFound", result.Error.Code);
        Assert.Empty(database.Context.Conversations);
    }

    [Fact]
    public async Task Handle_CreatesAGroupWithATrimmedTitle()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");
        var mohamed = database.AddUser("mohamed@example.com", "Mohamed");

        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var result = await handler.Handle(
            new CreateConversationCommand([sara.Id, mohamed.Id], "  Launch Room  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var conversation = await database.Context.Conversations
            .Include(c => c.Participants)
            .SingleAsync(c => c.Id == result.Value);

        Assert.True(conversation.IsGroup);
        Assert.Equal("Launch Room", conversation.Title);
        Assert.Equal(3, conversation.Participants.Count);
    }

    [Fact]
    public async Task Handle_RejectsAGroupWithoutATitle()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");
        var mohamed = database.AddUser("mohamed@example.com", "Mohamed");

        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var result = await handler.Handle(
            new CreateConversationCommand([sara.Id, mohamed.Id], "   "),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConversationErrors.TitleRequired.Code, result.Error.Code);
        Assert.Empty(database.Context.Conversations);
    }

    [Fact]
    public async Task Handle_RejectsATitleOverTheDatabaseLimit()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");
        var mohamed = database.AddUser("mohamed@example.com", "Mohamed");

        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var title = new string('x', ConversationConstants.TitleMaxLength + 1);

        var result = await handler.Handle(
            new CreateConversationCommand([sara.Id, mohamed.Id], title),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConversationErrors.TitleTooLong.Code, result.Error.Code);
        Assert.Empty(database.Context.Conversations);
    }

    [Fact]
    public async Task Handle_RejectsMoreParticipantsThanTheLimit()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");

        var participantIds = new List<Guid>();

        for (var index = 0; index < ConversationConstants.MaxParticipants + 1; index++)
            participantIds.Add(database.AddUser($"user{index}@example.com", $"User {index}").Id);

        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var result = await handler.Handle(
            new CreateConversationCommand(participantIds, "Too big"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConversationErrors.TooManyParticipants.Code, result.Error.Code);
        Assert.Empty(database.Context.Conversations);
    }

    [Fact]
    public async Task Handle_RejectsBotDmsWhenBotsAreDisabled()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var ada = database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var handler = new CreateConversationCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            Options.Create(new BotOptions { Enabled = false }));

        var result = await handler.Handle(
            new CreateConversationCommand([ada.Id]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConversationErrors.BotsDisabled.Code, result.Error.Code);
        Assert.Empty(database.Context.Conversations);
    }

    [Fact]
    public async Task Validator_RejectsBlankTitlesLongTitlesAndOversizedParticipantLists()
    {
        var validator = new CreateConversationCommandValidator();

        var missingTitle = await validator.ValidateAsync(
            new CreateConversationCommand([Guid.NewGuid(), Guid.NewGuid()], "   "));

        Assert.False(missingTitle.IsValid);
        Assert.Contains(missingTitle.Errors, error => error.ErrorMessage.Contains("title", StringComparison.OrdinalIgnoreCase));

        var tooLong = await validator.ValidateAsync(
            new CreateConversationCommand(
                [Guid.NewGuid(), Guid.NewGuid()],
                new string('x', ConversationConstants.TitleMaxLength + 1)));

        Assert.False(tooLong.IsValid);

        var tooMany = await validator.ValidateAsync(
            new CreateConversationCommand(
                Enumerable.Range(0, ConversationConstants.MaxParticipants + 1).Select(_ => Guid.NewGuid()).ToList(),
                "Too big"));

        Assert.False(tooMany.IsValid);

        var atTheLimit = await validator.ValidateAsync(
            new CreateConversationCommand(
                Enumerable.Range(0, ConversationConstants.MaxParticipants).Select(_ => Guid.NewGuid()).ToList(),
                "Exactly at the limit"));

        Assert.True(atTheLimit.IsValid);
    }
}
