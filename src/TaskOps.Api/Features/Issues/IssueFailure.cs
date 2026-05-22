namespace TaskOps.Api.Features.Issues;

public enum IssueFailure
{
    None = 0,
    Validation = 1,
    Unauthorized = 2,
    Forbidden = 3,
    NotFound = 4,
    ProjectNotFound = 5,
    AssigneeNotOrganizationMember = 6,
    IssueNumberConflict = 7
}
