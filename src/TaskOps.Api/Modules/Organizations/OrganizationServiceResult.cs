namespace TaskOps.Api.Modules.Organizations;

public enum OrganizationFailure
{
    None = 0,
    Validation = 1,
    Unauthorized = 2,
    NotFound = 3,
    DuplicateSlug = 4,
    UserNotFound = 5,
    DuplicateMember = 6,
    CannotRemoveLastOwner = 7
}
