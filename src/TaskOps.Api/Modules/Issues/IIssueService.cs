using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Modules.Issues;

public interface IIssueService
{
    Task<ServiceResult<PagedResponse<IssueListItemResponse>, IssueFailure>> ListIssuesAsync(
        Guid organizationId,
        IssueListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<IssueResponse, IssueFailure>> CreateIssueAsync(
        Guid organizationId,
        CreateIssueRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<IssueResponse, IssueFailure>> GetIssueAsync(
        Guid organizationId,
        Guid issueId,
        CancellationToken cancellationToken);

    Task<ServiceResult<IssueResponse, IssueFailure>> UpdateIssueAsync(
        Guid organizationId,
        Guid issueId,
        UpdateIssueRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<IssueResponse, IssueFailure>> AssignIssueAsync(
        Guid organizationId,
        Guid issueId,
        AssignIssueRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<IssueResponse, IssueFailure>> ChangeStatusAsync(
        Guid organizationId,
        Guid issueId,
        ChangeIssueStatusRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<IssueResponse, IssueFailure>> ChangePriorityAsync(
        Guid organizationId,
        Guid issueId,
        ChangeIssuePriorityRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<IssueResponse, IssueFailure>> SetDueDateAsync(
        Guid organizationId,
        Guid issueId,
        SetIssueDueDateRequest request,
        CancellationToken cancellationToken);
}
