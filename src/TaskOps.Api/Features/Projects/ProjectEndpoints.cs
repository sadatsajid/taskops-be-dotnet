using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Features.Projects;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organizations/{organizationId:guid}/projects")
            .RequireAuthorization()
            .WithTags("Projects");

        group.MapGet("", ListProjectsAsync)
            .WithName("ListProjects");

        group.MapPost("", CreateProjectAsync)
            .WithName("CreateProject");

        group.MapGet("/{projectId:guid}", GetProjectAsync)
            .WithName("GetProject");

        group.MapPut("/{projectId:guid}", UpdateProjectAsync)
            .WithName("UpdateProject");

        group.MapPost("/{projectId:guid}/archive", ArchiveProjectAsync)
            .WithName("ArchiveProject");

        return endpoints;
    }

    private static async Task<IResult> ListProjectsAsync(
        Guid organizationId,
        [AsParameters] PageRequest page,
        bool? includeArchived,
        IProjectService projectService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await projectService.ListProjectsAsync(organizationId, page, includeArchived ?? false, cancellationToken);
        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> CreateProjectAsync(
        Guid organizationId,
        CreateProjectRequest request,
        IProjectService projectService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await projectService.CreateProjectAsync(organizationId, request, cancellationToken);

        return result.IsSuccess(ProjectFailure.None)
            ? EndpointResults.Created($"/api/organizations/{organizationId}/projects/{result.Value!.Id}", result.Value, httpContext)
            : ToFailureResult(result);
    }

    private static async Task<IResult> GetProjectAsync(
        Guid organizationId,
        Guid projectId,
        IProjectService projectService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await projectService.GetProjectAsync(organizationId, projectId, cancellationToken);
        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid organizationId,
        Guid projectId,
        UpdateProjectRequest request,
        IProjectService projectService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await projectService.UpdateProjectAsync(organizationId, projectId, request, cancellationToken);
        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> ArchiveProjectAsync(
        Guid organizationId,
        Guid projectId,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var result = await projectService.ArchiveProjectAsync(organizationId, projectId, cancellationToken);

        return result.Failure == ProjectFailure.None
            ? Results.NoContent()
            : ToFailureResult(result);
    }

    private static IResult ToOkResult<T>(ServiceResult<T, ProjectFailure> result, HttpContext httpContext)
    {
        return result.IsSuccess(ProjectFailure.None)
            ? EndpointResults.Ok(result.Value!, httpContext)
            : ToFailureResult(result);
    }

    private static IResult ToFailureResult<T>(ServiceResult<T, ProjectFailure> result)
    {
        return result.Failure switch
        {
            ProjectFailure.Validation => EndpointResults.ValidationProblem(result.Errors),
            ProjectFailure.Unauthorized => Results.Unauthorized(),
            ProjectFailure.Forbidden => Results.Problem(
                title: "Forbidden.",
                detail: "The current user does not have the required organization role.",
                statusCode: StatusCodes.Status403Forbidden),
            ProjectFailure.NotFound => Results.NotFound(),
            ProjectFailure.DuplicateKey => Results.Problem(
                title: "Duplicate project key.",
                detail: "A project with this key already exists in the organization.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
