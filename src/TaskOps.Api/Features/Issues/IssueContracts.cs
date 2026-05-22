using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Features.Issues;

public sealed record CreateIssueRequest(
    Guid ProjectId,
    string? Title,
    string? Description,
    string? Priority,
    Guid? AssigneeId,
    DateOnly? DueDate);

public sealed record UpdateIssueRequest(
    string? Title,
    string? Description);

public sealed record AssignIssueRequest(Guid? AssigneeId);

public sealed record ChangeIssueStatusRequest(string? Status);

public sealed record ChangeIssuePriorityRequest(string? Priority);

public sealed record SetIssueDueDateRequest(DateOnly? DueDate);

public sealed class IssueListQuery
{
    public int Offset { get; init; } = 0;

    public int Limit { get; init; } = 50;

    public string? Status { get; init; }

    public string? Priority { get; init; }

    public Guid? AssigneeId { get; init; }

    public Guid? ProjectId { get; init; }

    public DateOnly? CreatedFrom { get; init; }

    public DateOnly? CreatedTo { get; init; }

    public DateOnly? DueFrom { get; init; }

    public DateOnly? DueTo { get; init; }

    public string? Search { get; init; }

    public string? Sort { get; init; }
}

public sealed record IssueAssigneeResponse(
    Guid MemberId,
    Guid UserId,
    string DisplayName,
    string Email);

public sealed record IssueListItemResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ProjectId,
    string ProjectKey,
    int Number,
    string Key,
    string Title,
    string Status,
    string Priority,
    IssueAssigneeResponse? Assignee,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record IssueResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ProjectId,
    string ProjectKey,
    int Number,
    string Key,
    string Title,
    string? Description,
    string Status,
    string Priority,
    IssueAssigneeResponse? Assignee,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static string FormatStatus(IssueStatus status) => status.ToString();

    public static string FormatPriority(IssuePriority priority) => priority.ToString();
}
