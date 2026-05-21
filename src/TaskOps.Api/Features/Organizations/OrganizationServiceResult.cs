namespace TaskOps.Api.Features.Organizations;

public enum OrganizationFailure
{
    None = 0,
    Validation = 1,
    Unauthorized = 2,
    Forbidden = 3,
    NotFound = 4,
    DuplicateSlug = 5,
    UserNotFound = 6,
    DuplicateMember = 7,
    CannotRemoveLastOwner = 8
}

public sealed record OrganizationServiceResult<T>(
    T? Value,
    OrganizationFailure Failure,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static OrganizationServiceResult<T> Success(T value) => new(value, OrganizationFailure.None);

    public static OrganizationServiceResult<T> Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(default, OrganizationFailure.Validation, errors);

    public static OrganizationServiceResult<T> Failed(OrganizationFailure failure) => new(default, failure);
}
