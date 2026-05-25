namespace TaskOps.Api.Modules.Projects;

public enum ProjectFailure
{
    None = 0,
    Validation = 1,
    Unauthorized = 2,
    Forbidden = 3,
    NotFound = 4,
    DuplicateKey = 5
}
