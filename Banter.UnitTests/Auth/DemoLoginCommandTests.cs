using Banter.Application.Features.Auth;
using Banter.Infrastructure.Services;
using Banter.UnitTests.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace Banter.UnitTests.Auth;

public class DemoLoginCommandTests
{
    [Fact]
    public async Task Handle_RefusesToSignInWhenDemoModeIsDisabledBeforeTouchingIdentity()
    {
        await using var database = TestDatabase.Create();

        var handler = new DemoLoginCommandHandler(
            database.Context,
            null!,
            new SeededDemoUserDirectory(),
            Options.Create(new DemoOptions { Enabled = false }));

        var result = await handler.Handle(new DemoLoginCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Demo.Disabled", result.Error.Code);
    }
}
