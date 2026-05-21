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
