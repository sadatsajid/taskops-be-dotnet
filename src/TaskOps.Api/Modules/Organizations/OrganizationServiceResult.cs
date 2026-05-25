namespace TaskOps.Api.Modules.Organizations;

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
