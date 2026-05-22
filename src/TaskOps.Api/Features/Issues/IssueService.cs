using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Api;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Features.Issues;

public sealed class IssueService(
    TaskOpsDbContext dbContext,
    IOrganizationAccessService organizationAccess,
    IValidator<IssueListQuery> issueListQueryValidator,
    IValidator<CreateIssueRequest> createIssueValidator,
    IValidator<UpdateIssueRequest> updateIssueValidator,
    IValidator<ChangeIssueStatusRequest> changeStatusValidator,
    IValidator<ChangeIssuePriorityRequest> changePriorityValidator) : IIssueService
{
    private static readonly OrganizationRole[] IssueManagers =
    [
        OrganizationRole.Owner,
        OrganizationRole.Admin,
        OrganizationRole.ProjectManager
    ];

    public async Task<ServiceResult<PagedResponse<IssueListItemResponse>, IssueFailure>> ListIssuesAsync(
        Guid organizationId,
        IssueListQuery query,
        CancellationToken cancellationToken)
    {
        var access = await organizationAccess.RequireMembershipAsync(organizationId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Failure<PagedResponse<IssueListItemResponse>>(ToIssueFailure(access.Status));
        }

        var validation = await issueListQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<PagedResponse<IssueListItemResponse>>(validation.ToErrorDictionary());
        }

        var status = IssueValidation.TryParseNamedEnum<IssueStatus>(query.Status, out var parsedStatus)
            ? parsedStatus
            : (IssueStatus?)null;
        var priority = IssueValidation.TryParseNamedEnum<IssuePriority>(query.Priority, out var parsedPriority)
            ? parsedPriority
            : (IssuePriority?)null;
        _ = TryParseSort(query.Sort, out var sort);
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

        var rows = await ApplySort(issuesQuery, sort)
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
            .Select(ToListItemResponse)
            .ToList();

        return Success(new PagedResponse<IssueListItemResponse>(
            items,
            offset,
            limit,
            rows.Count > limit));
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> CreateIssueAsync(
        Guid organizationId,
        CreateIssueRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireIssueManagerAsync(organizationId, cancellationToken);
        if (access != IssueFailure.None)
        {
            return Failure<IssueResponse>(access);
        }

        var validation = await createIssueValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<IssueResponse>(validation.ToErrorDictionary());
        }

        _ = IssueValidation.TryParseNamedEnum<IssuePriority>(request.Priority, out var priority);
        Guid issueId;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var project = await dbContext.Projects
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "Projects"
                    WHERE "OrganizationId" = {organizationId}
                      AND "Id" = {request.ProjectId}
                      AND NOT "IsArchived"
                    FOR UPDATE
                    """)
                .Select(project => new { project.Id })
                .FirstOrDefaultAsync(cancellationToken);
            if (project is null)
            {
                return Failure<IssueResponse>(IssueFailure.ProjectNotFound);
            }

            if (request.AssigneeId is { } assigneeId && !await IsOrganizationMemberAsync(organizationId, assigneeId, cancellationToken))
            {
                return Failure<IssueResponse>(IssueFailure.AssigneeNotOrganizationMember);
            }

            var nextNumber = await GetNextIssueNumberAsync(organizationId, project.Id, cancellationToken);
            var issue = new Issue
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ProjectId = project.Id,
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
        CancellationToken cancellationToken)
    {
        var access = await organizationAccess.RequireMembershipAsync(organizationId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Failure<IssueResponse>(ToIssueFailure(access.Status));
        }

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> UpdateIssueAsync(
        Guid organizationId,
        Guid issueId,
        UpdateIssueRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireIssueManagerAsync(organizationId, cancellationToken);
        if (access != IssueFailure.None)
        {
            return Failure<IssueResponse>(access);
        }

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

        issue.Title = request.Title!.Trim();
        issue.Description = IssueValidation.NormalizeOptional(request.Description);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> AssignIssueAsync(
        Guid organizationId,
        Guid issueId,
        AssignIssueRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireIssueManagerAsync(organizationId, cancellationToken);
        if (access != IssueFailure.None)
        {
            return Failure<IssueResponse>(access);
        }

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

        issue.AssigneeId = request.AssigneeId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> ChangeStatusAsync(
        Guid organizationId,
        Guid issueId,
        ChangeIssueStatusRequest request,
        CancellationToken cancellationToken)
    {
        var access = await organizationAccess.RequireMembershipAsync(organizationId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Failure<IssueResponse>(ToIssueFailure(access.Status));
        }

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

        if (!CanManageIssues(access.Membership!.Role) && !CanAssignedDeveloperChangeStatus(access.Membership, issue.AssigneeId))
        {
            return Failure<IssueResponse>(IssueFailure.Forbidden);
        }

        issue.Status = status;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> ChangePriorityAsync(
        Guid organizationId,
        Guid issueId,
        ChangeIssuePriorityRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireIssueManagerAsync(organizationId, cancellationToken);
        if (access != IssueFailure.None)
        {
            return Failure<IssueResponse>(access);
        }

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

        issue.Priority = priority;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    public async Task<ServiceResult<IssueResponse, IssueFailure>> SetDueDateAsync(
        Guid organizationId,
        Guid issueId,
        SetIssueDueDateRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireIssueManagerAsync(organizationId, cancellationToken);
        if (access != IssueFailure.None)
        {
            return Failure<IssueResponse>(access);
        }

        var issue = await dbContext.Issues.FirstOrDefaultAsync(
            issue => issue.OrganizationId == organizationId && issue.Id == issueId,
            cancellationToken);
        if (issue is null)
        {
            return Failure<IssueResponse>(IssueFailure.NotFound);
        }

        issue.DueDate = request.DueDate;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadIssueResponseAsync(organizationId, issueId, cancellationToken);
    }

    private async Task<ServiceResult<IssueResponse, IssueFailure>> LoadIssueResponseAsync(
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

        return row is null
            ? Failure<IssueResponse>(IssueFailure.NotFound)
            : Success(ToIssueResponse(row));
    }

    private async Task<IssueFailure> RequireIssueManagerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var access = await organizationAccess.RequireAnyRoleAsync(organizationId, IssueManagers, cancellationToken);
        return access.IsAllowed ? IssueFailure.None : ToIssueFailure(access.Status);
    }

    private async Task<bool> IsOrganizationMemberAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken) =>
        await dbContext.OrganizationMembers.AnyAsync(
            member => member.OrganizationId == organizationId && member.Id == memberId,
            cancellationToken);

    private async Task<int> GetNextIssueNumberAsync(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var currentMax = await dbContext.Issues
            .Where(issue => issue.OrganizationId == organizationId && issue.ProjectId == projectId)
            .MaxAsync(issue => (int?)issue.Number, cancellationToken);

        return (currentMax ?? 0) + 1;
    }

    private static bool TryParseSort(string? value, out IssueSort sort)
    {
        sort = IssueSort.CreatedAtDescending;
        var normalized = IssueValidation.NormalizeOptional(value);
        if (normalized is null)
        {
            return true;
        }

        sort = normalized switch
        {
            "createdAt" or "created" => IssueSort.CreatedAtAscending,
            "-createdAt" or "-created" => IssueSort.CreatedAtDescending,
            "dueDate" or "due" => IssueSort.DueDateAscending,
            "-dueDate" or "-due" => IssueSort.DueDateDescending,
            "priority" => IssueSort.PriorityAscending,
            "-priority" => IssueSort.PriorityDescending,
            "status" => IssueSort.StatusAscending,
            "-status" => IssueSort.StatusDescending,
            "title" => IssueSort.TitleAscending,
            "-title" => IssueSort.TitleDescending,
            "number" => IssueSort.NumberAscending,
            "-number" => IssueSort.NumberDescending,
            _ => IssueSort.Invalid
        };

        return sort != IssueSort.Invalid;
    }

    private static IOrderedQueryable<Issue> ApplySort(IQueryable<Issue> query, IssueSort sort)
    {
        return sort switch
        {
            IssueSort.CreatedAtAscending => query.OrderBy(issue => issue.CreatedAt).ThenBy(issue => issue.Id),
            IssueSort.DueDateAscending => query
                .OrderBy(issue => issue.DueDate == null)
                .ThenBy(issue => issue.DueDate)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.DueDateDescending => query
                .OrderBy(issue => issue.DueDate == null)
                .ThenByDescending(issue => issue.DueDate)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.PriorityAscending => query
                .OrderBy(issue =>
                    issue.Priority == IssuePriority.Low ? 1 :
                    issue.Priority == IssuePriority.Medium ? 2 :
                    issue.Priority == IssuePriority.High ? 3 :
                    4)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.PriorityDescending => query
                .OrderByDescending(issue =>
                    issue.Priority == IssuePriority.Low ? 1 :
                    issue.Priority == IssuePriority.Medium ? 2 :
                    issue.Priority == IssuePriority.High ? 3 :
                    4)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.StatusAscending => query
                .OrderBy(issue =>
                    issue.Status == IssueStatus.Todo ? 1 :
                    issue.Status == IssueStatus.InProgress ? 2 :
                    issue.Status == IssueStatus.InReview ? 3 :
                    4)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.StatusDescending => query
                .OrderByDescending(issue =>
                    issue.Status == IssueStatus.Todo ? 1 :
                    issue.Status == IssueStatus.InProgress ? 2 :
                    issue.Status == IssueStatus.InReview ? 3 :
                    4)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.TitleAscending => query.OrderBy(issue => issue.Title).ThenBy(issue => issue.Id),
            IssueSort.TitleDescending => query.OrderByDescending(issue => issue.Title).ThenBy(issue => issue.Id),
            IssueSort.NumberAscending => query.OrderBy(issue => issue.Project.Key).ThenBy(issue => issue.Number),
            IssueSort.NumberDescending => query.OrderByDescending(issue => issue.Project.Key).ThenByDescending(issue => issue.Number),
            _ => query.OrderByDescending(issue => issue.CreatedAt).ThenByDescending(issue => issue.Id)
        };
    }

    private static bool CanManageIssues(OrganizationRole role) => IssueManagers.Contains(role);

    private static bool CanAssignedDeveloperChangeStatus(OrganizationMember membership, Guid? assigneeId) =>
        membership.Role == OrganizationRole.Developer && assigneeId == membership.Id;

    private static DateTimeOffset ToUtcStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static IssueListItemResponse ToListItemResponse(IssueListProjection row) =>
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

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static IssueResponse ToIssueResponse(IssueProjection row) =>
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

    private static IssueFailure ToIssueFailure(OrganizationAccessStatus status)
    {
        return status switch
        {
            OrganizationAccessStatus.Unauthorized => IssueFailure.Unauthorized,
            OrganizationAccessStatus.NotFound => IssueFailure.NotFound,
            OrganizationAccessStatus.Forbidden => IssueFailure.Forbidden,
            _ => IssueFailure.None
        };
    }

    private static ServiceResult<T, IssueFailure> Success<T>(T value) =>
        ServiceResult<T, IssueFailure>.Success(value, IssueFailure.None);

    private static ServiceResult<T, IssueFailure> Validation<T>(IReadOnlyDictionary<string, string[]> errors) =>
        ServiceResult<T, IssueFailure>.Validation(IssueFailure.Validation, errors);

    private static ServiceResult<T, IssueFailure> Failure<T>(IssueFailure failure) =>
        ServiceResult<T, IssueFailure>.Failed(failure);

    private sealed record IssueListProjection(
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

    private sealed record IssueProjection(
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

    private enum IssueSort
    {
        Invalid,
        CreatedAtAscending,
        CreatedAtDescending,
        DueDateAscending,
        DueDateDescending,
        PriorityAscending,
        PriorityDescending,
        StatusAscending,
        StatusDescending,
        TitleAscending,
        TitleDescending,
        NumberAscending,
        NumberDescending
    }
}
