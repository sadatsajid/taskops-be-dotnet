using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Modules.Projects;

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
        return EndpointResults.OkOrFailure(result, ProjectFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> CreateProjectAsync(
        Guid organizationId,
        CreateProjectRequest request,
        IProjectService projectService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await projectService.CreateProjectAsync(organizationId, request, cancellationToken);

        return EndpointResults.CreatedOrFailure(
            result,
            ProjectFailure.None,
            project => $"/api/organizations/{organizationId}/projects/{project.Id}",
            httpContext,
            ToFailureResult);
    }

    private static async Task<IResult> GetProjectAsync(
        Guid organizationId,
        Guid projectId,
        IProjectService projectService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await projectService.GetProjectAsync(organizationId, projectId, cancellationToken);
        return EndpointResults.OkOrFailure(result, ProjectFailure.None, httpContext, ToFailureResult);
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
        return EndpointResults.OkOrFailure(result, ProjectFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> ArchiveProjectAsync(
        Guid organizationId,
        Guid projectId,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var result = await projectService.ArchiveProjectAsync(organizationId, projectId, cancellationToken);

        return EndpointResults.NoContentOrFailure(result, ProjectFailure.None, ToFailureResult);
    }

    private static IResult ToFailureResult<T>(ServiceResult<T, ProjectFailure> result)
    {
        return result.Failure switch
        {
            ProjectFailure.Validation => EndpointResults.ValidationProblem(result.Errors),
            ProjectFailure.Unauthorized => EndpointResults.Unauthorized(),
            ProjectFailure.Forbidden => EndpointResults.ForbiddenProblem(
                "The current user does not have the required organization role."),
            ProjectFailure.NotFound => EndpointResults.NotFound(),
            ProjectFailure.DuplicateKey => EndpointResults.ConflictProblem(
                "Duplicate project key.",
                "A project with this key already exists in the organization."),
            _ => EndpointResults.InternalServerError()
        };
    }
}
