using Banter.SharedKernel;
using Xunit;

namespace Banter.UnitTests.SharedKernel;

public class ResultTests
{
    [Fact]
    public void Success_ExposesTheValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_ExposesTheErrorAndThrowsOnValue()
    {
        var error = Error.NotFound("Users.NotFound", "User not found");
        var result = Result.Failure<int>(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void NullValue_ImplicitlyConvertsToAFailure()
    {
        Result<string> result = (string)null!;

        Assert.False(result.IsSuccess);
        Assert.Equal(Error.NullValue, result.Error);
    }

    [Fact]
    public void NonNullValue_ImplicitlyConvertsToASuccess()
    {
        Result<string> result = "value";

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public void DefaultResult_ThrowsWhenErrorIsRead()
    {
        Result result = default;

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }
}
