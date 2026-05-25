using Banter.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Extensions;

public static class CustomResults
{
    public static ProblemHttpResult Problem<TValue>(Result<TValue> result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A problem Can not be Created for a successful result");
        }

        return CreateProblem(result.Error);
    }
    public static ProblemHttpResult Problem(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A problem Can not be Created for a successful result");
        }

        return CreateProblem(result.Error);
    }

    private static ProblemHttpResult CreateProblem(Error error)
        => TypedResults.Problem(
            title: GetTitle(error),
            detail: GetDetail(error),
            type: GetType(error.Type),
            statusCode: GetStatusCode(error.Type),
            extensions: GetErrors(error));

    private static string GetTitle(Error error) =>
            error.Type switch
            {
                ErrorType.MultiError => error.Code,
                ErrorType.Problem => error.Code,
                ErrorType.NotFound => error.Code,
                ErrorType.Conflict => error.Code,
                ErrorType.Forbidden => error.Code,
                _ => "Server failure"
            };

    private static string GetDetail(Error error) =>
        error.Type switch
        {
            ErrorType.MultiError => error.Description,
            ErrorType.Problem => error.Description,
            ErrorType.NotFound => error.Description,
            ErrorType.Conflict => error.Description,
            ErrorType.Forbidden => error.Description,
            _ => "An unexpected error occurred"
        };

    private static string GetType(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.MultiError or ErrorType.Problem => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            ErrorType.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            ErrorType.Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            ErrorType.Forbidden => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.3",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

    private static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.MultiError or ErrorType.Problem => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

    private static Dictionary<string, object?>? GetErrors(Error error)
    {
        if (error is not MultiError multiError)
        {
            return null;
        }

        return new Dictionary<string, object?>
            {
                { "errors", multiError.Errors }
            };
    }

}

