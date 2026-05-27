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
    IValidator<IssueListQuery> issueListQueryValidator,
    IValidator<CreateIssueRequest> createIssueValidator,
    IValidator<UpdateIssueRequest> updateIssueValidator,
    IValidator<ChangeIssueStatusRequest> changeStatusValidator,
    IValidator<ChangeIssuePriorityRequest> changePriorityValidator) : IIssueService
{
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
        var response = await IssueQueries.GetAsync(dbContext, organizationId, issueId, cancellationToken);

        return response is null
            ? Failure<IssueResponse>(IssueFailure.NotFound)
            : Success(response);
    }

    private async Task<bool> IsOrganizationMemberAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken) =>
        await dbContext.OrganizationMembers.AnyAsync(
            member => member.OrganizationId == organizationId && member.Id == memberId,
            cancellationToken);

    private static ServiceResult<T, IssueFailure> Success<T>(T value) =>
        ServiceResult<T, IssueFailure>.Success(value, IssueFailure.None);

    private static ServiceResult<T, IssueFailure> Validation<T>(IReadOnlyDictionary<string, string[]> errors) =>
        ServiceResult<T, IssueFailure>.Validation(IssueFailure.Validation, errors);

    private static ServiceResult<T, IssueFailure> Failure<T>(IssueFailure failure) =>
        ServiceResult<T, IssueFailure>.Failed(failure);
}
