using FluentValidation.Results;

namespace TaskOps.Api.Shared.Api;

public static class ValidationResultExtensions
{
    public static IReadOnlyDictionary<string, string[]> ToErrorDictionary(this ValidationResult validationResult) =>
        validationResult.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());
}
