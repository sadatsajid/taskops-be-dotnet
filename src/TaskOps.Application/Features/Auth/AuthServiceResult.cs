namespace TaskOps.Application.Features.Auth;

public enum AuthFailure
{
    None,
    Validation,
    DuplicateEmail,
    InvalidCredentials,
    Unauthorized,
    NotFound
}
