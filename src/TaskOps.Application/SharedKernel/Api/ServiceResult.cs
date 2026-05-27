namespace TaskOps.Application.SharedKernel.Api;

public sealed record ServiceResult<T, TFailure>(
    T? Value,
    TFailure Failure,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public bool IsSuccess(TFailure noneFailure) =>
        EqualityComparer<TFailure>.Default.Equals(Failure, noneFailure) && Value is not null;

    public static ServiceResult<T, TFailure> Success(T value, TFailure noneFailure) =>
        new(value, noneFailure);

    public static ServiceResult<T, TFailure> Validation(
        TFailure validationFailure,
        IReadOnlyDictionary<string, string[]> errors) =>
        new(default, validationFailure, errors);

    public static ServiceResult<T, TFailure> Failed(TFailure failure) => new(default, failure);
}
