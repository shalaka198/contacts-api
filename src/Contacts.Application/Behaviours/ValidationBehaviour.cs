using FluentValidation;
using MediatR;
using Contacts.Application.Common;

namespace Contacts.Application.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that runs FluentValidation before every handler.
/// Eliminates the need for manual validation calls in each handler.
/// Validation failures return a Result.Failure rather than throwing exceptions.
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators) =>
        _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        // Build a validation error with all field failures
        var errors = failures
            .Select(f => $"{f.PropertyName}: {f.ErrorMessage}")
            .ToArray();

        var errorDescription = string.Join("; ", errors);

        // Attempt to return a Result<T> failure via reflection
        // This keeps the behaviour generic without needing a common constraint
        var resultType = typeof(TResponse);

        if (resultType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(
                new Error("Validation.Failed", errorDescription));
        }

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var innerType = resultType.GetGenericArguments()[0];
            var failureMethod = resultType.GetMethod("Failure")!;
            var errorObj = new Error("Validation.Failed", errorDescription);
            return (TResponse)failureMethod.Invoke(null, [errorObj])!;
        }

        // Fallback: throw for non-Result response types
        throw new ValidationException(failures);
    }
}
