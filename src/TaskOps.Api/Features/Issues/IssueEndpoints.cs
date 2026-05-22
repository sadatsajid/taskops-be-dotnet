using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Features.Issues;

public static class IssueEndpoints
{
    public static IEndpointRouteBuilder MapIssueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organizations/{organizationId:guid}/issues")
            .RequireAuthorization()
            .WithTags("Issues");

        group.MapGet("", ListIssuesAsync)
            .WithName("ListIssues");

        group.MapPost("", CreateIssueAsync)
            .WithName("CreateIssue");

        group.MapGet("/{issueId:guid}", GetIssueAsync)
            .WithName("GetIssue");

        group.MapPut("/{issueId:guid}", UpdateIssueAsync)
            .WithName("UpdateIssue");

        group.MapPut("/{issueId:guid}/assignment", AssignIssueAsync)
            .WithName("AssignIssue");

        group.MapPut("/{issueId:guid}/status", ChangeStatusAsync)
            .WithName("ChangeIssueStatus");

        group.MapPut("/{issueId:guid}/priority", ChangePriorityAsync)
            .WithName("ChangeIssuePriority");

        group.MapPut("/{issueId:guid}/due-date", SetDueDateAsync)
            .WithName("SetIssueDueDate");

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
        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> CreateIssueAsync(
        Guid organizationId,
        CreateIssueRequest request,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.CreateIssueAsync(organizationId, request, cancellationToken);

        return result.IsSuccess(IssueFailure.None)
            ? EndpointResults.Created($"/api/organizations/{organizationId}/issues/{result.Value!.Id}", result.Value, httpContext)
            : ToFailureResult(result);
    }

    private static async Task<IResult> GetIssueAsync(
        Guid organizationId,
        Guid issueId,
        IIssueService issueService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await issueService.GetIssueAsync(organizationId, issueId, cancellationToken);
        return ToOkResult(result, httpContext);
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
        return ToOkResult(result, httpContext);
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
        return ToOkResult(result, httpContext);
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
        return ToOkResult(result, httpContext);
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
        return ToOkResult(result, httpContext);
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
        return ToOkResult(result, httpContext);
    }

    private static IResult ToOkResult<T>(ServiceResult<T, IssueFailure> result, HttpContext httpContext)
    {
        return result.IsSuccess(IssueFailure.None)
            ? EndpointResults.Ok(result.Value!, httpContext)
            : ToFailureResult(result);
    }

    private static IResult ToFailureResult<T>(ServiceResult<T, IssueFailure> result)
    {
        return result.Failure switch
        {
            IssueFailure.Validation => EndpointResults.ValidationProblem(result.Errors),
            IssueFailure.Unauthorized => Results.Unauthorized(),
            IssueFailure.Forbidden => Results.Problem(
                title: "Forbidden.",
                detail: "The current user does not have permission to modify this issue.",
                statusCode: StatusCodes.Status403Forbidden),
            IssueFailure.NotFound => Results.NotFound(),
            IssueFailure.ProjectNotFound => Results.Problem(
                title: "Project not found.",
                detail: "The issue project does not exist in this organization.",
                statusCode: StatusCodes.Status404NotFound),
            IssueFailure.AssigneeNotOrganizationMember => Results.Problem(
                title: "Invalid assignee.",
                detail: "The assignee must be a member of the issue organization.",
                statusCode: StatusCodes.Status400BadRequest),
            IssueFailure.IssueNumberConflict => Results.Problem(
                title: "Issue number conflict.",
                detail: "A concurrent issue creation used the next issue number. Retry the request.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
