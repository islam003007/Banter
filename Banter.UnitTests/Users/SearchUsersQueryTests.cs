using Banter.Application.Features.Users;
using Banter.Application.Bots;
using Banter.UnitTests.TestSupport;
using Microsoft.Extensions.Options;
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
    public async Task Handle_IsCaseInsensitiveForDisplayNameAndEmail()
    {
        await using var database = TestDatabase.Create();

        database.AddUser("ada@example.com", "Ada Lovelace");
        database.AddUser("grace@example.com", "Grace Hopper");

        await database.SaveAsync();

        var handler = new SearchUsersHandler(
            database.Context,
            new FakePresenceService());

        var byDisplayName = await handler.Handle(
            new SearchUsersQuery("ADA", null, 10),
            CancellationToken.None);

        Assert.True(byDisplayName.IsSuccess);
        Assert.Equal("Ada Lovelace", Assert.Single(byDisplayName.Value.Users).DisplayName);

        var byEmail = await handler.Handle(
            new SearchUsersQuery("ADA@EXAMPLE.COM", null, 10),
            CancellationToken.None);

        Assert.True(byEmail.IsSuccess);
        Assert.Equal("Ada Lovelace", Assert.Single(byEmail.Value.Users).DisplayName);
    }

    [Fact]
    public async Task Validator_RejectsWhitespaceOnlySearchTerms()
    {
        var validator = new SearchUsersQueryValidator();

        var result = await validator.ValidateAsync(new SearchUsersQuery("   ", null, 10));

        Assert.False(result.IsValid);
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

    [Fact]
    public async Task Handle_HidesBotAccountsWhenBotsAreDisabled()
    {
        await using var database = TestDatabase.Create();

        database.AddUser("ahmed@example.com", "Ahmed");
        database.AddBot("ada@bots.local", "Ada");

        await database.SaveAsync();

        var handler = new SearchUsersHandler(
            database.Context,
            new FakePresenceService(),
            Options.Create(new BotOptions { Enabled = false }));

        var result = await handler.Handle(
            new SearchUsersQuery("Ada", null, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Users);
    }
}
