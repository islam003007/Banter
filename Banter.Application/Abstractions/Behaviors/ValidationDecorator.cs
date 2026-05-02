using Banter.Application.Abstractions.Messaging;
using Banter.SharedKernel;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Banter.Application.Abstractions.Behaviors;


internal class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseRequest
    where TResponse : IResult<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ValidationFailure[] failures = await ValidateAsync(request, _validators, cancellationToken);

        if (failures.Length == 0)
        {
            return await next(cancellationToken);
        }

        return TResponse.Failure(createValidationError(failures));
    }
    private static async Task<ValidationFailure[]> ValidateAsync<T>(T command,
       IEnumerable<IValidator<T>> validators,
       CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return [];
        }

        var context = new ValidationContext<T>(command);

        ValidationResult[] validationResults = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        ValidationFailure[] validationFailures = validationResults
            .Where(validationResult => !validationResult.IsValid)
            .SelectMany(validationResult => validationResult.Errors)
            .ToArray();

        return validationFailures;
    }

    private static ValidationError createValidationError(ValidationFailure[] validationFailures) =>
        new(validationFailures.Select(f => Error.Problem(f.ErrorCode, f.ErrorMessage)));
}