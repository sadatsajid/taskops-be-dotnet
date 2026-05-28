using TaskOps.Application.Modules.Issues;
using TaskOps.Domain.Modules.Issues;

namespace TaskOps.Infrastructure.Modules.Issues;

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

    public static IssueCommentResponse ToCommentResponse(IssueCommentProjection row) =>
        new(
            row.Id,
            row.OrganizationId,
            row.IssueId,
            new IssueCommentAuthorResponse(
                row.AuthorMemberId,
                row.AuthorUserId,
                row.AuthorDisplayName,
                row.AuthorEmail),
            row.Body,
            row.CreatedAt,
            row.UpdatedAt);

    public static IssueActivityResponse ToActivityResponse(IssueActivityProjection row) =>
        new(
            row.Id,
            row.OrganizationId,
            row.IssueId,
            row.Type.ToString(),
            ToActivityActorResponse(row.ActorMemberId, row.ActorUserId, row.ActorDisplayName, row.ActorEmail),
            row.Field,
            row.OldValue,
            row.NewValue,
            row.CommentId,
            row.CreatedAt);

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

    private static IssueActivityActorResponse? ToActivityActorResponse(
        Guid? actorMemberId,
        Guid? actorUserId,
        string? actorDisplayName,
        string? actorEmail) =>
        actorMemberId is null || actorUserId is null || actorDisplayName is null || actorEmail is null
            ? null
            : new IssueActivityActorResponse(
                actorMemberId.Value,
                actorUserId.Value,
                actorDisplayName,
                actorEmail);

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

internal sealed record IssueCommentProjection(
    Guid Id,
    Guid OrganizationId,
    Guid IssueId,
    Guid AuthorMemberId,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string AuthorEmail,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record IssueActivityProjection(
    Guid Id,
    Guid OrganizationId,
    Guid IssueId,
    IssueActivityType Type,
    Guid? ActorMemberId,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string? ActorEmail,
    string? Field,
    string? OldValue,
    string? NewValue,
    Guid? CommentId,
    DateTimeOffset CreatedAt);

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
