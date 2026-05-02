namespace Banter.SharedKernel;

public enum ErrorType
{
    Conflict,
    Problem, // maps to a generic 400 error.
    NotFound,
    Forbidden,
    MultiError, // a collection of errors
}
