namespace TaskOps.Api.Modules.Issues;

public enum IssueFailure
{
    None = 0,
    Validation = 1,
    Forbidden = 2,
    NotFound = 3,
    ProjectNotFound = 4,
    AssigneeNotOrganizationMember = 5,
    IssueNumberConflict = 6
}
