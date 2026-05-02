namespace Banter.SharedKernel;

public interface IResult<TSelf> where TSelf : IResult<TSelf>
{
    bool IsSuccess { get; }
    Error Error { get; }
    static abstract TSelf Failure(Error error);
}