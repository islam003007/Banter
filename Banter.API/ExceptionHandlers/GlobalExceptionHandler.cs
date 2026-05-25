using Banter.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Banter.API.ExceptionHandlers;

internal class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> _logger, IProblemDetailsService _problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is AppException appException)
        {
            _logger.LogError(appException, "Application exception occurred. Code = {Code}, Extensions = {@Extensions}",
                appException.Code,
                appException.Extensions);
        }
        else
        {
            _logger.LogError(exception, "Unhandled exception occurred.");
        }

        httpContext.Response.StatusCode = 500;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext()
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
                Title = "Server Failure",
                Detail = "An unexpected error occurred"
            }
        });
    }
}
