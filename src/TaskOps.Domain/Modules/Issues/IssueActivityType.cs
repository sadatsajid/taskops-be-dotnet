namespace TaskOps.Domain.Modules.Issues;

public enum IssueActivityType
{
    IssueCreated = 1,
    DetailsUpdated = 2,
    StatusChanged = 3,
    AssigneeChanged = 4,
    PriorityChanged = 5,
    DueDateChanged = 6,
    CommentAdded = 7,
    CommentEdited = 8,
    CommentDeleted = 9
}
