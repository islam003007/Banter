using Banter.Infrastructure.Services;
using Xunit;

namespace Banter.UnitTests.Demo;

public class SeededDemoUserDirectoryTests
{
    private static readonly string[] _seededEmails =
    [
        "ahmed@example.com",
        "sara@example.com",
        "mohamed@example.com",
        "nour@example.com",
        "omar@example.com",
        "fatma@example.com"
    ];

    [Fact]
    public void Emails_AreExactlyTheSeededUsersInSeedOrder()
    {
        var directory = new SeededDemoUserDirectory();

        Assert.Equal(_seededEmails, directory.Emails);
    }

    [Theory]
    [InlineData("ahmed@example.com")]
    [InlineData("SARA@EXAMPLE.COM")]
    [InlineData("Fatma@Example.com")]
    public void IsAllowed_AcceptsSeededEmailsCaseInsensitively(string email)
    {
        var directory = new SeededDemoUserDirectory();

        Assert.True(directory.IsAllowed(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("stranger@example.com")]
    [InlineData("ada@bots.local")]
    [InlineData("milo@bots.local")]
    [InlineData("nova@bots.local")]
    public void IsAllowed_RejectsAnythingOutsideTheSeed(string? email)
    {
        var directory = new SeededDemoUserDirectory();

        Assert.False(directory.IsAllowed(email));
    }
}
