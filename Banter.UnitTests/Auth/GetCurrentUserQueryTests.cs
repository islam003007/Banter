using Banter.Application.Features.Auth;
using Banter.SharedKernel;
using Banter.UnitTests.TestSupport;
using Xunit;

namespace Banter.UnitTests.Auth;

public class GetCurrentUserQueryTests
{
    [Fact]
    public async Task Handle_ReturnsTheCurrentUsersProfile()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");

        await database.SaveAsync();

        var handler = new GetCurrentUserQueryHandler(
            database.Context,
            new FakeUserContext(ahmed.Id));

        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ahmed.Id, result.Value.Id);
        Assert.Equal("ahmed@example.com", result.Value.Email);
        Assert.Equal("Ahmed", result.Value.DisplayName);
        Assert.Null(result.Value.ProfilePictureUrl);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenTheAuthenticatedUserNoLongerExists()
    {
        await using var database = TestDatabase.Create();

        var handler = new GetCurrentUserQueryHandler(
            database.Context,
            new FakeUserContext(Guid.NewGuid()));

        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Users.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
