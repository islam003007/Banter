using Banter.Application.Features.Users;
using Banter.UnitTests.TestSupport;
using Xunit;

namespace Banter.UnitTests.Users;

public class SearchUsersQueryTests
{
    [Fact]
    public async Task Handle_ReturnsTheBotFlagForBotAccounts()
    {
        await using var database = TestDatabase.Create();

        database.AddUser("ahmed@example.com", "Ahmed");
        var ada = database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var handler = new SearchUsersHandler(
            database.Context,
            new FakePresenceService(ada.Id));

        var result = await handler.Handle(
            new SearchUsersQuery("Ada", null, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var user = Assert.Single(result.Value.Users);

        Assert.Equal(ada.Id, user.Id);
        Assert.True(user.IsBot);
        Assert.True(user.IsOnline);
    }

    [Fact]
    public async Task Handle_ReturnsFalseForHumanAccounts()
    {
        await using var database = TestDatabase.Create();

        database.AddUser("ahmed@example.com", "Ahmed");
        database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var handler = new SearchUsersHandler(
            database.Context,
            new FakePresenceService());

        var result = await handler.Handle(
            new SearchUsersQuery("Ahmed", null, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(Assert.Single(result.Value.Users).IsBot);
    }
}
