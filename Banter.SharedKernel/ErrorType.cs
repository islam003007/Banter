namespace Banter.SharedKernel;

public enum ErrorType
{
    Problem, // maps to a generic 400 error.
    Conflict,
    NotFound,
    Forbidden,
    MultiError, // a collection of errors
}
