using Banter.Application.Abstractions.Messaging;
using Banter.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Banter.Application.Abstractions.Behaviors;


internal class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseRequest
    where TResponse : IResult<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        _logger.LogInformation("Processing Request {Request}", requestName);

        TResponse result = await next(cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Completed Request {Request}", requestName);
        }
        else
        {
            _logger.LogInformation("Completed Request {Request} with error {@Error}", requestName, result.Error);
        }

        return result;
    }
}
