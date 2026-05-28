using Microsoft.EntityFrameworkCore;
using TaskOps.Application.Modules.Issues;
using TaskOps.Application.SharedKernel.Api;
using TaskOps.Domain.Modules.Issues;
using TaskOps.Infrastructure.Persistence;

namespace TaskOps.Infrastructure.Modules.Issues;

internal static class IssueQueries
{
    public static async Task<PagedResponse<IssueListItemResponse>> ListAsync(
        TaskOpsDbContext dbContext,
        Guid organizationId,
        IssueListQuery query,
        CancellationToken cancellationToken)
    {
        var status = IssueValidation.TryParseNamedEnum<IssueStatus>(query.Status, out var parsedStatus)
            ? parsedStatus
            : (IssueStatus?)null;
        var priority = IssueValidation.TryParseNamedEnum<IssuePriority>(query.Priority, out var parsedPriority)
            ? parsedPriority
            : (IssuePriority?)null;
        _ = IssueSorting.TryParse(query.Sort, out var sort);
        var page = new PageRequest(query.Offset, query.Limit);
        var limit = page.SafeLimit;
        var offset = page.SafeOffset;
        var search = IssueValidation.NormalizeOptional(query.Search);

        var issuesQuery = dbContext.Issues
            .AsNoTracking()
            .Where(issue => issue.OrganizationId == organizationId);

        if (query.ProjectId is { } projectId)
        {
            issuesQuery = issuesQuery.Where(issue => issue.ProjectId == projectId);
        }

        if (status is { } statusFilter)
        {
            issuesQuery = issuesQuery.Where(issue => issue.Status == statusFilter);
        }

        if (priority is { } priorityFilter)
        {
            issuesQuery = issuesQuery.Where(issue => issue.Priority == priorityFilter);
        }

        if (query.AssigneeId is { } assigneeId)
        {
            issuesQuery = issuesQuery.Where(issue => issue.AssigneeId == assigneeId);
        }

        if (query.CreatedFrom is { } createdFrom)
        {
            var createdFromUtc = ToUtcStart(createdFrom);
            issuesQuery = issuesQuery.Where(issue => issue.CreatedAt >= createdFromUtc);
        }

        if (query.CreatedTo is { } createdTo)
        {
            var createdToExclusiveUtc = ToUtcStart(createdTo.AddDays(1));
            issuesQuery = issuesQuery.Where(issue => issue.CreatedAt < createdToExclusiveUtc);
        }

        if (query.DueFrom is { } dueFrom)
        {
            issuesQuery = issuesQuery.Where(issue => issue.DueDate >= dueFrom);
        }

        if (query.DueTo is { } dueTo)
        {
            issuesQuery = issuesQuery.Where(issue => issue.DueDate <= dueTo);
        }

        if (search is not null)
        {
            var pattern = $"%{EscapeLikePattern(search)}%";
            issuesQuery = issuesQuery.Where(issue =>
                EF.Functions.ILike(issue.Title, pattern, "\\") ||
                (issue.Description != null && EF.Functions.ILike(issue.Description, pattern, "\\")));
        }

        var rows = await IssueSorting.Apply(issuesQuery, sort)
            .Skip(offset)
            .Take(limit + 1)
            .Select(issue => new IssueListProjection(
                issue.Id,
                issue.OrganizationId,
                issue.ProjectId,
                issue.Project.Key,
                issue.Number,
                issue.Title,
                issue.Status,
                issue.Priority,
                issue.Assignee == null ? null : issue.Assignee.Id,
                issue.Assignee == null ? null : issue.Assignee.UserId,
                issue.Assignee == null ? null : issue.Assignee.User.DisplayName,
                issue.Assignee == null ? null : issue.Assignee.User.Email,
                issue.DueDate,
                issue.CreatedAt,
                issue.UpdatedAt))
            .ToListAsync(cancellationToken);

        var items = rows
            .Take(limit)
            .Select(IssueMappings.ToListItemResponse)
            .ToList();

        return new PagedResponse<IssueListItemResponse>(
            items,
            offset,
            limit,
            rows.Count > limit);
    }

    public static async Task<IssueResponse?> GetAsync(
        TaskOpsDbContext dbContext,
        Guid organizationId,
        Guid issueId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Issues
            .AsNoTracking()
            .Where(issue => issue.OrganizationId == organizationId && issue.Id == issueId)
            .Select(issue => new IssueProjection(
                issue.Id,
                issue.OrganizationId,
                issue.ProjectId,
                issue.Project.Key,
                issue.Number,
                issue.Title,
                issue.Description,
                issue.Status,
                issue.Priority,
                issue.Assignee == null ? null : issue.Assignee.Id,
                issue.Assignee == null ? null : issue.Assignee.UserId,
                issue.Assignee == null ? null : issue.Assignee.User.DisplayName,
                issue.Assignee == null ? null : issue.Assignee.User.Email,
                issue.DueDate,
                issue.CreatedAt,
                issue.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : IssueMappings.ToIssueResponse(row);
    }

    public static async Task<PagedResponse<IssueCommentResponse>?> ListCommentsAsync(
        TaskOpsDbContext dbContext,
        Guid organizationId,
        Guid issueId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (!await IssueExistsAsync(dbContext, organizationId, issueId, cancellationToken))
        {
            return null;
        }

        var limit = page.SafeLimit;
        var offset = page.SafeOffset;
        var rows = await dbContext.IssueComments
            .AsNoTracking()
            .Where(comment =>
                comment.OrganizationId == organizationId &&
                comment.IssueId == issueId &&
                comment.DeletedAt == null)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Skip(offset)
            .Take(limit + 1)
            .Select(comment => new IssueCommentProjection(
                comment.Id,
                comment.OrganizationId,
                comment.IssueId,
                comment.Author.Id,
                comment.Author.UserId,
                comment.Author.User.DisplayName,
                comment.Author.User.Email,
                comment.Body,
                comment.CreatedAt,
                comment.UpdatedAt))
            .ToListAsync(cancellationToken);

        var items = rows
            .Take(limit)
            .Select(IssueMappings.ToCommentResponse)
            .ToList();

        return new PagedResponse<IssueCommentResponse>(
            items,
            offset,
            limit,
            rows.Count > limit);
    }

    public static async Task<IssueCommentResponse?> GetCommentAsync(
        TaskOpsDbContext dbContext,
        Guid organizationId,
        Guid issueId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.IssueComments
            .AsNoTracking()
            .Where(comment =>
                comment.OrganizationId == organizationId &&
                comment.IssueId == issueId &&
                comment.Id == commentId &&
                comment.DeletedAt == null)
            .Select(comment => new IssueCommentProjection(
                comment.Id,
                comment.OrganizationId,
                comment.IssueId,
                comment.Author.Id,
                comment.Author.UserId,
                comment.Author.User.DisplayName,
                comment.Author.User.Email,
                comment.Body,
                comment.CreatedAt,
                comment.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : IssueMappings.ToCommentResponse(row);
    }

    public static async Task<PagedResponse<IssueActivityResponse>?> ListActivityAsync(
        TaskOpsDbContext dbContext,
        Guid organizationId,
        Guid issueId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (!await IssueExistsAsync(dbContext, organizationId, issueId, cancellationToken))
        {
            return null;
        }

        var limit = page.SafeLimit;
        var offset = page.SafeOffset;
        var rows = await dbContext.IssueActivities
            .AsNoTracking()
            .Where(activity => activity.OrganizationId == organizationId && activity.IssueId == issueId)
            .OrderByDescending(activity => activity.CreatedAt)
            .ThenByDescending(activity => activity.Id)
            .Skip(offset)
            .Take(limit + 1)
            .Select(activity => new IssueActivityProjection(
                activity.Id,
                activity.OrganizationId,
                activity.IssueId,
                activity.Type,
                activity.Actor == null ? null : activity.Actor.Id,
                activity.Actor == null ? null : activity.Actor.UserId,
                activity.Actor == null ? null : activity.Actor.User.DisplayName,
                activity.Actor == null ? null : activity.Actor.User.Email,
                activity.Field,
                activity.OldValue,
                activity.NewValue,
                activity.CommentId,
                activity.CreatedAt))
            .ToListAsync(cancellationToken);

        var items = rows
            .Take(limit)
            .Select(IssueMappings.ToActivityResponse)
            .ToList();

        return new PagedResponse<IssueActivityResponse>(
            items,
            offset,
            limit,
            rows.Count > limit);
    }

    private static async Task<bool> IssueExistsAsync(
        TaskOpsDbContext dbContext,
        Guid organizationId,
        Guid issueId,
        CancellationToken cancellationToken) =>
        await dbContext.Issues
            .AsNoTracking()
            .AnyAsync(issue => issue.OrganizationId == organizationId && issue.Id == issueId, cancellationToken);

    private static DateTimeOffset ToUtcStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
