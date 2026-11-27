using Banter.Application.Bots;
using Banter.Application.Features.Messages;
using Banter.Domain.Conversations;
using Banter.SharedKernel;
using Banter.UnitTests.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace Banter.UnitTests.Messages;

public class GetMessagesQueryTests
{
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

        var handler = new GetMessagesQueryHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            Options.Create(new BotOptions { Enabled = false }));

        var result = await handler.Handle(
            new GetMessagesQuery(conversation.Id, null, 20),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conversations.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
