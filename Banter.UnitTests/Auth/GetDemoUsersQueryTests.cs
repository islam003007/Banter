using Banter.Application.Features.Auth;
using Banter.Infrastructure.Services;
using Banter.UnitTests.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace Banter.UnitTests.Auth;

public class GetDemoUsersQueryTests
{
    [Fact]
    public async Task Handle_ReturnsOnlySeededUsersInSeedOrder()
    {
        await using var database = TestDatabase.Create();

        var directory = new SeededDemoUserDirectory();

        for (var index = 0; index < directory.Emails.Count; index++)
        {
            database.AddUser(directory.Emails[index], $"Demo {index}");
        }

        database.AddUser("stranger@example.com", "Stranger");

        await database.SaveAsync();

        var handler = new GetDemoUsersQueryHandler(
            database.Context,
            directory,
            Options.Create(new DemoOptions { Enabled = true }));

        var result = await handler.Handle(new GetDemoUsersQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(directory.Emails, result.Value.Select(user => user.Email));
    }

    [Fact]
    public async Task Handle_RefusesToReturnUsersWhenDemoModeIsDisabled()
    {
        await using var database = TestDatabase.Create();

        var handler = new GetDemoUsersQueryHandler(
            database.Context,
            new SeededDemoUserDirectory(),
            Options.Create(new DemoOptions { Enabled = false }));

        var result = await handler.Handle(new GetDemoUsersQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Demo.Disabled", result.Error.Code);
    }
}
