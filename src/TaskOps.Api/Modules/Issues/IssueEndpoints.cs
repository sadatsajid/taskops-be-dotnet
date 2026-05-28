using TaskOps.Api.Modules.Organizations.Access;
using TaskOps.Application.Modules.Issues;
using TaskOps.Application.SharedKernel.Api;
using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Modules.Issues;

public static class IssueEndpoints
{
    public static IEndpointRouteBuilder MapIssueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organizations/{organizationId:guid}/issues")
            .WithTags("Issues");

        group.MapGet("", ListIssuesAsync)
            .WithName("ListIssues")
            .RequireAuthorization(OrganizationPolicies.Member);

        group.MapPost("", CreateIssueAsync)
            .WithName("CreateIssue")
            .RequireAuthorization(OrganizationPolicies.ProjectManagement);

        group.MapGet("/{issueId:guid}", GetIssueAsync)
            .WithName("GetIssue")
            .RequireAuthorization(OrganizationPolicies.Member);

        group.MapPut("/{issueId:guid}", UpdateIssueAsync)
            .WithName("UpdateIssue")
            .RequireAuthorization(OrganizationPolicies.ProjectManagement);

        group.MapPut("/{issueId:guid}/assignment", AssignIssueAsync)
            .WithName("AssignIssue")
            .RequireAuthorization(OrganizationPolicies.ProjectManagement);

        group.MapPut("/{issueId:guid}/status", ChangeStatusAsync)
            .WithName("ChangeIssueStatus")
            .RequireAuthorization(OrganizationPolicies.Member);

        group.MapPut("/{issueId:guid}/priority", ChangePriorityAsync)
            .WithName("ChangeIssuePriority")
            .RequireAuthorization(OrganizationPolicies.ProjectManagement);

        group.MapPut("/{issueId:guid}/due-date", SetDueDateAsync)
            .WithName("SetIssueDueDate")
            .RequireAuthorization(OrganizationPolicies.ProjectManagement);

        group.MapGet("/{issueId:guid}/comments", ListIssueCommentsAsync)
            .WithName("ListIssueComments")
            .RequireAuthorization(OrganizationPolicies.Member);

        group.MapPost("/{issueId:guid}/comments", CreateIssueCommentAsync)
            .WithName("CreateIssueComment")
            .RequireAuthorization(OrganizationPolicies.Member);

        group.MapPut("/{issueId:guid}/comments/{commentId:guid}", UpdateIssueCommentAsync)
            .WithName("UpdateIssueComment")
            .RequireAuthorization(OrganizationPolicies.Member);

        group.MapDelete("/{issueId:guid}/comments/{commentId:guid}", DeleteIssueCommentAsync)
            .WithName("DeleteIssueComment")
            .RequireAuthorization(OrganizationPolicies.Member);

        group.MapGet("/{issueId:guid}/activity", ListIssueActivityAsync)
            .WithName("ListIssueActivity")
            .RequireAuthorization(OrganizationPolicies.Member);

        return endpoints;
    }

    private static async Task<IResult> ListIssuesAsync(
        Guid organizationId,
        int? offset,
        int? limit,
        string? status,
        string? priority,
        Guid? assigneeId,
        Guid? projectId,
        DateOnly? createdFrom,
        DateOnly? createdTo,
        DateOnly? dueFrom,
        DateOnly? dueTo,
        string? search,
        string? sort,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new IssueListQuery
        {
            Offset = offset ?? 0,
            Limit = limit ?? 50,
            Status = status,
            Priority = priority,
            AssigneeId = assigneeId,
            ProjectId = projectId,
            CreatedFrom = createdFrom,
            CreatedTo = createdTo,
            DueFrom = dueFrom,
            DueTo = dueTo,
            Search = search,
            Sort = sort
        };
        var result = await issueService.ListIssuesAsync(organizationId, query, cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> CreateIssueAsync(
        Guid organizationId,
        CreateIssueRequest request,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.CreateIssueAsync(organizationId, request, cancellationToken);

        return EndpointResults.CreatedOrFailure(
            result,
            IssueFailure.None,
            issue => $"/api/organizations/{organizationId}/issues/{issue.Id}",
            httpContext,
            ToFailureResult);
    }

    private static async Task<IResult> GetIssueAsync(
        Guid organizationId,
        Guid issueId,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.GetIssueAsync(organizationId, issueId, cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> UpdateIssueAsync(
        Guid organizationId,
        Guid issueId,
        UpdateIssueRequest request,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.UpdateIssueAsync(organizationId, issueId, request, cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> AssignIssueAsync(
        Guid organizationId,
        Guid issueId,
        AssignIssueRequest request,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.AssignIssueAsync(organizationId, issueId, request, cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid organizationId,
        Guid issueId,
        ChangeIssueStatusRequest request,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.ChangeStatusAsync(organizationId, issueId, request, cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> ChangePriorityAsync(
        Guid organizationId,
        Guid issueId,
        ChangeIssuePriorityRequest request,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.ChangePriorityAsync(organizationId, issueId, request, cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> SetDueDateAsync(
        Guid organizationId,
        Guid issueId,
        SetIssueDueDateRequest request,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.SetDueDateAsync(organizationId, issueId, request, cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> ListIssueCommentsAsync(
        Guid organizationId,
        Guid issueId,
        [AsParameters] PageRequest page,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.ListIssueCommentsAsync(organizationId, issueId, page, cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> CreateIssueCommentAsync(
        Guid organizationId,
        Guid issueId,
        CreateIssueCommentRequest request,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.CreateIssueCommentAsync(organizationId, issueId, request, cancellationToken);

        return EndpointResults.CreatedOrFailure(
            result,
            IssueFailure.None,
            comment => $"/api/organizations/{organizationId}/issues/{issueId}/comments/{comment.Id}",
            httpContext,
            ToFailureResult);
    }

    private static async Task<IResult> UpdateIssueCommentAsync(
        Guid organizationId,
        Guid issueId,
        Guid commentId,
        UpdateIssueCommentRequest request,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.UpdateIssueCommentAsync(
            organizationId,
            issueId,
            commentId,
            request,
            cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> DeleteIssueCommentAsync(
        Guid organizationId,
        Guid issueId,
        Guid commentId,
        IIssueService issueService,
        CancellationToken cancellationToken)
    {
        var result = await issueService.DeleteIssueCommentAsync(organizationId, issueId, commentId, cancellationToken);
        return EndpointResults.NoContentOrFailure(result, IssueFailure.None, ToFailureResult);
    }

    private static async Task<IResult> ListIssueActivityAsync(
        Guid organizationId,
        Guid issueId,
        [AsParameters] PageRequest page,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.ListIssueActivityAsync(organizationId, issueId, page, cancellationToken);
        return EndpointResults.OkOrFailure(result, IssueFailure.None, httpContext, ToFailureResult);
    }

    private static IResult ToFailureResult<T>(ServiceResult<T, IssueFailure> result)
    {
        return result.Failure switch
        {
            IssueFailure.Validation => EndpointResults.ValidationProblem(result.Errors),
            IssueFailure.Forbidden => EndpointResults.ForbiddenProblem(
                "The current user does not have permission to modify this issue resource."),
            IssueFailure.NotFound => EndpointResults.NotFound(),
            IssueFailure.ProjectNotFound => EndpointResults.NotFoundProblem(
                "Project not found.",
                "The issue project does not exist in this organization."),
            IssueFailure.AssigneeNotOrganizationMember => EndpointResults.BadRequestProblem(
                "Invalid assignee.",
                "The assignee must be a member of the issue organization."),
            IssueFailure.IssueNumberConflict => EndpointResults.ConflictProblem(
                "Issue number conflict.",
                "A concurrent issue creation used the next issue number. Retry the request."),
            _ => EndpointResults.InternalServerError()
        };
    }
}
