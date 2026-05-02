using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Banter.SharedKernel;

// this type is supposed be created via the factory methods only.
public readonly struct Result : IResult<Result>
{
    public Error Error =>
        _error is null
        ? throw new InvalidOperationException("Uninitialized Result.")
        : _error;
    public bool IsSuccess { get; }

    private readonly Error _error;
    private Result(bool isSuccess, Error? error = default)
    {
        if (isSuccess && error is not null && error != Error.None)
            throw new ArgumentException("Success result cannot have an error.", nameof(error));

        if (!isSuccess && (error is null || error == Error.None))
            throw new ArgumentException("Failure result must have an error.", nameof(error));

        IsSuccess = isSuccess;
        _error = error is null ? Error.None : error;
    }

    public static Result<TValue> Success<TValue>(TValue value) => value is not null ? new(value, true) :
        throw new InvalidOperationException("Invalid Result: Success result cannot hold null");
    public static Result<TValue> Failure<TValue>(Error error) => new(default!, false, error);

    public static Result Success() => new(true);
    public static Result Failure(Error error) => new(false, error);
}


public readonly struct Result<TValue> : IResult<Result<TValue>>
{
    private readonly TValue? _value;
    public Error Error =>
       _error is null
       ? throw new InvalidOperationException("Uninitialized Result.")
       : _error;
    public bool IsSuccess { get; }

    private readonly Error _error;
    internal Result(TValue value, bool isSuccess, Error? error = default)
    {
        if (isSuccess && error is not null && error != Error.None)
            throw new ArgumentException("Success result cannot have an error.", nameof(error));

        if (!isSuccess && (error is null || error == Error.None))
            throw new ArgumentException("Failure result must have an error.", nameof(error));

        _value = value;
        IsSuccess = isSuccess;
        _error = error is null ? Error.None : error;
    }

    public TValue Value => IsSuccess
    ? _value!
    : throw new InvalidOperationException("Cannot access Value of a failure result.");

    public static implicit operator Result<TValue>(TValue value) => value is not null ?
        Result.Success(value) : Result.Failure<TValue>(Error.NullValue);

    public static Result<TValue> Failure(Error error) => new(default!, false, error);

    // might add implict cast from Error
}

