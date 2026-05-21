namespace TaskOps.Api.Features.Auth;

public enum AuthFailure
{
    None,
    Validation,
    DuplicateEmail,
    InvalidCredentials,
    Unauthorized,
    NotFound
}

public sealed record AuthServiceResult<T>(
    T? Value,
    AuthFailure Failure,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static AuthServiceResult<T> Success(T value) => new(value, AuthFailure.None);

    public static AuthServiceResult<T> Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(default, AuthFailure.Validation, errors);

    public static AuthServiceResult<T> Failed(AuthFailure failure) => new(default, failure);
}
