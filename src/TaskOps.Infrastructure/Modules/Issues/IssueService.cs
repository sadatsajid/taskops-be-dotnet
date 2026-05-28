using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TaskOps.Application.Modules.Issues;
using TaskOps.Application.Modules.Organizations.Access;
using TaskOps.Application.SharedKernel.Api;
using TaskOps.Domain.Modules.Issues;
using TaskOps.Infrastructure.Persistence;

namespace TaskOps.Infrastructure.Modules.Issues;

public sealed class IssueService(
    TaskOpsDbContext dbContext,
    IOrganizationContext organizationContext,
    TimeProvider timeProvider,
    IValidator<IssueListQuery> issueListQueryValidator,
    IValidator<CreateIssueRequest> createIssueValidator,
    IValidator<UpdateIssueRequest> updateIssueValidator,
    IValidator<ChangeIssueStatusRequest> changeStatusValidator,
    IValidator<ChangeIssuePriorityRequest> changePriorityValidator,
    IValidator<CreateIssueCommentRequest> createCommentValidator,
    IValidator<UpdateIssueCommentRequest> updateCommentValidator) : IIssueService
{
    private static readonly object Empty = new();

    public async Task<ServiceResult<PagedResponse<IssueListItemResponse>, IssueFailure>> ListIssuesAsync(
        Guid organizationId,
        IssueListQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await issueListQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<PagedResponse<IssueListItemResponse>>(validation.ToErrorDictionary());
        }

        var response = await IssueQueries.ListAsync(dbContext, organizationId, query, cancellationToken);
        return Success(response);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> CreateIssueAsync(
        Guid organizationId,
        CreateIssueRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createIssueValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<IssueResponse>(validation.ToErrorDictionary());
        }

        _ = IssueValidation.TryParseNamedEnum<IssuePriority>(request.Priority, out var priority);
        Guid issueId;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var projectId = await IssueNumbering.TryLockActiveProjectAsync(
                dbContext,
                organizationId,
                request.ProjectId,
                cancellationToken);
            if (projectId is null)
            {
                return Failure<IssueResponse>(IssueFailure.ProjectNotFound);
            }

            if (request.AssigneeId is { } assigneeId && !await IsOrganizationMemberAsync(organizationId, assigneeId, cancellationToken))
            {
                return Failure<IssueResponse>(IssueFailure.AssigneeNotOrganizationMember);
            }

            var nextNumber = await IssueNumbering.GetNextNumberAsync(
                dbContext,
                organizationId,
                projectId.Value,
                cancellationToken);
            var issue = new Issue
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ProjectId = projectId.Value,
                Number = nextNumber,
                Title = request.Title!.Trim(),
                Description = IssueValidation.NormalizeOptional(request.Description),
                Status = IssueStatus.Todo,
                Priority = priority,
                AssigneeId = request.AssigneeId,
                DueDate = request.DueDate
            };

            issueId = issue.Id;
            dbContext.Issues.Add(issue);
            dbContext.IssueActivities.Add(NewActivity(
                organizationId,
                issueId,
                IssueActivityType.IssueCreated));

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "IX_Issues_OrganizationId_ProjectId_Number"))
            {
                return Failure<IssueResponse>(IssueFailure.IssueNumberConflict);
            }
        }

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> GetIssueAsync(
        Guid organizationId,
        Guid issueId,
        CancellationToken cancellationToken) =>
        await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);

    public async Task<ServiceResult<IssueResponse, IssueFailure>> UpdateIssueAsync(
        Guid organizationId,
        Guid issueId,
        UpdateIssueRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateIssueValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<IssueResponse>(validation.ToErrorDictionary());
        }

        var issue = await dbContext.Issues.FirstOrDefaultAsync(
            issue => issue.OrganizationId == organizationId && issue.Id == issueId,
            cancellationToken);
        if (issue is null)
        {
            return Failure<IssueResponse>(IssueFailure.NotFound);
        }

        var title = request.Title!.Trim();
        var description = IssueValidation.NormalizeOptional(request.Description);
        var changed = issue.Title != title || issue.Description != description;

        issue.Title = title;
        issue.Description = description;

        if (changed)
        {
            dbContext.IssueActivities.Add(NewActivity(
                organizationId,
                issueId,
                IssueActivityType.DetailsUpdated,
                "details"));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> AssignIssueAsync(
        Guid organizationId,
        Guid issueId,
        AssignIssueRequest request,
        CancellationToken cancellationToken)
    {
        var issue = await dbContext.Issues.FirstOrDefaultAsync(
            issue => issue.OrganizationId == organizationId && issue.Id == issueId,
            cancellationToken);
        if (issue is null)
        {
            return Failure<IssueResponse>(IssueFailure.NotFound);
        }

        if (request.AssigneeId is { } assigneeId && !await IsOrganizationMemberAsync(organizationId, assigneeId, cancellationToken))
        {
            return Failure<IssueResponse>(IssueFailure.AssigneeNotOrganizationMember);
        }

        var changed = issue.AssigneeId != request.AssigneeId;
        var oldAssignee = changed
            ? await GetMemberDisplayValueAsync(organizationId, issue.AssigneeId, cancellationToken)
            : null;
        var newAssignee = changed
            ? await GetMemberDisplayValueAsync(organizationId, request.AssigneeId, cancellationToken)
            : null;

        issue.AssigneeId = request.AssigneeId;

        if (changed)
        {
            dbContext.IssueActivities.Add(NewActivity(
                organizationId,
                issueId,
                IssueActivityType.AssigneeChanged,
                "assignee",
                oldAssignee,
                newAssignee));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> ChangeStatusAsync(
        Guid organizationId,
        Guid issueId,
        ChangeIssueStatusRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await changeStatusValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<IssueResponse>(validation.ToErrorDictionary());
        }

        _ = IssueValidation.TryParseNamedEnum<IssueStatus>(request.Status, out var status);
        var issue = await dbContext.Issues.FirstOrDefaultAsync(
            issue => issue.OrganizationId == organizationId && issue.Id == issueId,
            cancellationToken);
        if (issue is null)
        {
            return Failure<IssueResponse>(IssueFailure.NotFound);
        }

        var membership = organizationContext.Membership;
        if (!IssueAccessPolicy.CanManageIssues(membership.Role) &&
            !IssueAccessPolicy.CanAssignedDeveloperChangeStatus(membership, issue.AssigneeId))
        {
            return Failure<IssueResponse>(IssueFailure.Forbidden);
        }

        var oldStatus = issue.Status;
        issue.Status = status;

        if (oldStatus != status)
        {
            dbContext.IssueActivities.Add(NewActivity(
                organizationId,
                issueId,
                IssueActivityType.StatusChanged,
                "status",
                oldStatus.ToString(),
                status.ToString()));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> ChangePriorityAsync(
        Guid organizationId,
        Guid issueId,
        ChangeIssuePriorityRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await changePriorityValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<IssueResponse>(validation.ToErrorDictionary());
        }

        _ = IssueValidation.TryParseNamedEnum<IssuePriority>(request.Priority, out var priority);
        var issue = await dbContext.Issues.FirstOrDefaultAsync(
            issue => issue.OrganizationId == organizationId && issue.Id == issueId,
            cancellationToken);
        if (issue is null)
        {
            return Failure<IssueResponse>(IssueFailure.NotFound);
        }

        var oldPriority = issue.Priority;
        issue.Priority = priority;

        if (oldPriority != priority)
        {
            dbContext.IssueActivities.Add(NewActivity(
                organizationId,
                issueId,
                IssueActivityType.PriorityChanged,
                "priority",
                oldPriority.ToString(),
                priority.ToString()));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> SetDueDateAsync(
        Guid organizationId,
        Guid issueId,
        SetIssueDueDateRequest request,
        CancellationToken cancellationToken)
    {
        var issue = await dbContext.Issues.FirstOrDefaultAsync(
            issue => issue.OrganizationId == organizationId && issue.Id == issueId,
            cancellationToken);
        if (issue is null)
        {
            return Failure<IssueResponse>(IssueFailure.NotFound);
        }

        var oldDueDate = issue.DueDate;
        issue.DueDate = request.DueDate;

        if (oldDueDate != request.DueDate)
        {
            dbContext.IssueActivities.Add(NewActivity(
                organizationId,
                issueId,
                IssueActivityType.DueDateChanged,
                "dueDate",
                FormatDate(oldDueDate),
                FormatDate(request.DueDate)));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<PagedResponse<IssueCommentResponse>, IssueFailure>> ListIssueCommentsAsync(
        Guid organizationId,
        Guid issueId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var response = await IssueQueries.ListCommentsAsync(dbContext, organizationId, issueId, page, cancellationToken);

        return response is null
            ? Failure<PagedResponse<IssueCommentResponse>>(IssueFailure.NotFound)
            : Success(response);
    }

    public async Task<ServiceResult<IssueCommentResponse, IssueFailure>> CreateIssueCommentAsync(
        Guid organizationId,
        Guid issueId,
        CreateIssueCommentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createCommentValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<IssueCommentResponse>(validation.ToErrorDictionary());
        }

        var issueExists = await dbContext.Issues
            .AsNoTracking()
            .AnyAsync(issue => issue.OrganizationId == organizationId && issue.Id == issueId, cancellationToken);
        if (!issueExists)
        {
            return Failure<IssueCommentResponse>(IssueFailure.NotFound);
        }

        var comment = new IssueComment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            IssueId = issueId,
            AuthorMemberId = organizationContext.Membership.Id,
            Body = request.Body!.Trim()
        };

        dbContext.IssueComments.Add(comment);
        dbContext.IssueActivities.Add(NewActivity(
            organizationId,
            issueId,
            IssueActivityType.CommentAdded,
            "comment",
            commentId: comment.Id));

        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadCommentResponseAsync(organizationId, issueId, comment.Id, cancellationToken);
    }

    public async Task<ServiceResult<IssueCommentResponse, IssueFailure>> UpdateIssueCommentAsync(
        Guid organizationId,
        Guid issueId,
        Guid commentId,
        UpdateIssueCommentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateCommentValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<IssueCommentResponse>(validation.ToErrorDictionary());
        }

        var comment = await dbContext.IssueComments.FirstOrDefaultAsync(
            comment =>
                comment.OrganizationId == organizationId &&
                comment.IssueId == issueId &&
                comment.Id == commentId &&
                comment.DeletedAt == null,
            cancellationToken);
        if (comment is null)
        {
            return Failure<IssueCommentResponse>(IssueFailure.NotFound);
        }

        if (!CanModifyComment(comment.AuthorMemberId))
        {
            return Failure<IssueCommentResponse>(IssueFailure.Forbidden);
        }

        var body = request.Body!.Trim();
        if (comment.Body != body)
        {
            comment.Body = body;
            dbContext.IssueActivities.Add(NewActivity(
                organizationId,
                issueId,
                IssueActivityType.CommentEdited,
                "comment",
                commentId: commentId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadCommentResponseAsync(organizationId, issueId, commentId, cancellationToken);
    }

    public async Task<ServiceResult<object, IssueFailure>> DeleteIssueCommentAsync(
        Guid organizationId,
        Guid issueId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var comment = await dbContext.IssueComments.FirstOrDefaultAsync(
            comment =>
                comment.OrganizationId == organizationId &&
                comment.IssueId == issueId &&
                comment.Id == commentId &&
                comment.DeletedAt == null,
            cancellationToken);
        if (comment is null)
        {
            return Failure<object>(IssueFailure.NotFound);
        }

        if (!CanModifyComment(comment.AuthorMemberId))
        {
            return Failure<object>(IssueFailure.Forbidden);
        }

        comment.DeletedAt = timeProvider.GetUtcNow();
        comment.DeletedByMemberId = organizationContext.Membership.Id;
        dbContext.IssueActivities.Add(NewActivity(
            organizationId,
            issueId,
            IssueActivityType.CommentDeleted,
            "comment",
            commentId: commentId));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Success(Empty);
    }

    public async Task<ServiceResult<PagedResponse<IssueActivityResponse>, IssueFailure>> ListIssueActivityAsync(
        Guid organizationId,
        Guid issueId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var response = await IssueQueries.ListActivityAsync(dbContext, organizationId, issueId, page, cancellationToken);

        return response is null
            ? Failure<PagedResponse<IssueActivityResponse>>(IssueFailure.NotFound)
            : Success(response);
    }

    private async Task<ServiceResult<IssueResponse, IssueFailure>> LoadIssueResponseAsync(
        Guid organizationId,
        Guid issueId,
        CancellationToken cancellationToken)
    {
        var response = await IssueQueries.GetAsync(dbContext, organizationId, issueId, cancellationToken);

        return response is null
            ? Failure<IssueResponse>(IssueFailure.NotFound)
            : Success(response);
    }

    private async Task<ServiceResult<IssueCommentResponse, IssueFailure>> LoadCommentResponseAsync(
        Guid organizationId,
        Guid issueId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var response = await IssueQueries.GetCommentAsync(dbContext, organizationId, issueId, commentId, cancellationToken);

        return response is null
            ? Failure<IssueCommentResponse>(IssueFailure.NotFound)
            : Success(response);
    }

    private async Task<bool> IsOrganizationMemberAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken) =>
        await dbContext.OrganizationMembers.AnyAsync(
            member => member.OrganizationId == organizationId && member.Id == memberId,
            cancellationToken);

    private async Task<string?> GetMemberDisplayValueAsync(
        Guid organizationId,
        Guid? memberId,
        CancellationToken cancellationToken)
    {
        if (memberId is null)
        {
            return null;
        }

        return await dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId && member.Id == memberId)
            .Select(member => member.User.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private bool CanModifyComment(Guid authorMemberId)
    {
        var membership = organizationContext.Membership;
        return membership.Id == authorMemberId || IssueAccessPolicy.CanManageIssues(membership.Role);
    }

    private IssueActivity NewActivity(
        Guid organizationId,
        Guid issueId,
        IssueActivityType type,
        string? field = null,
        string? oldValue = null,
        string? newValue = null,
        Guid? commentId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            IssueId = issueId,
            ActorMemberId = organizationContext.Membership.Id,
            Type = type,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            CommentId = commentId,
            CreatedAt = timeProvider.GetUtcNow()
        };

    private static string? FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static ServiceResult<T, IssueFailure> Success<T>(T value) =>
        ServiceResult<T, IssueFailure>.Success(value, IssueFailure.None);

    private static ServiceResult<T, IssueFailure> Validation<T>(IReadOnlyDictionary<string, string[]> errors) =>
        ServiceResult<T, IssueFailure>.Validation(IssueFailure.Validation, errors);

    private static ServiceResult<T, IssueFailure> Failure<T>(IssueFailure failure) =>
        ServiceResult<T, IssueFailure>.Failed(failure);
}
