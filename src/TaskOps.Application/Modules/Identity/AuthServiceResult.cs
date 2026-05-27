namespace TaskOps.Application.Modules.Identity;

public enum AuthFailure
{
    None,
    Validation,
    DuplicateEmail,
    InvalidCredentials,
    Unauthorized,
    NotFound
}
