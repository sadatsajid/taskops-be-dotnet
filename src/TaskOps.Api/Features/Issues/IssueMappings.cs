using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Features.Issues;

internal static class IssueMappings
{
    public static IssueListItemResponse ToListItemResponse(IssueListProjection row) =>
        new(
            row.Id,
            row.OrganizationId,
            row.ProjectId,
            row.ProjectKey,
            row.Number,
            FormatIssueKey(row.ProjectKey, row.Number),
            row.Title,
            IssueResponse.FormatStatus(row.Status),
            IssueResponse.FormatPriority(row.Priority),
            ToAssigneeResponse(row.AssigneeMemberId, row.AssigneeUserId, row.AssigneeDisplayName, row.AssigneeEmail),
            row.DueDate,
            row.CreatedAt,
            row.UpdatedAt);

    public static IssueResponse ToIssueResponse(IssueProjection row) =>
        new(
            row.Id,
            row.OrganizationId,
            row.ProjectId,
            row.ProjectKey,
            row.Number,
            FormatIssueKey(row.ProjectKey, row.Number),
            row.Title,
            row.Description,
            IssueResponse.FormatStatus(row.Status),
            IssueResponse.FormatPriority(row.Priority),
            ToAssigneeResponse(row.AssigneeMemberId, row.AssigneeUserId, row.AssigneeDisplayName, row.AssigneeEmail),
            row.DueDate,
            row.CreatedAt,
            row.UpdatedAt);

    private static IssueAssigneeResponse? ToAssigneeResponse(
        Guid? assigneeMemberId,
        Guid? assigneeUserId,
        string? assigneeDisplayName,
        string? assigneeEmail) =>
        assigneeMemberId is null || assigneeUserId is null || assigneeDisplayName is null || assigneeEmail is null
            ? null
            : new IssueAssigneeResponse(
                assigneeMemberId.Value,
                assigneeUserId.Value,
                assigneeDisplayName,
                assigneeEmail);

    private static string FormatIssueKey(string projectKey, int number) => $"{projectKey}-{number}";
}

internal sealed record IssueListProjection(
    Guid Id,
    Guid OrganizationId,
    Guid ProjectId,
    string ProjectKey,
    int Number,
    string Title,
    IssueStatus Status,
    IssuePriority Priority,
    Guid? AssigneeMemberId,
    Guid? AssigneeUserId,
    string? AssigneeDisplayName,
    string? AssigneeEmail,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record IssueProjection(
    Guid Id,
    Guid OrganizationId,
    Guid ProjectId,
    string ProjectKey,
    int Number,
    string Title,
    string? Description,
    IssueStatus Status,
    IssuePriority Priority,
    Guid? AssigneeMemberId,
    Guid? AssigneeUserId,
    string? AssigneeDisplayName,
    string? AssigneeEmail,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
