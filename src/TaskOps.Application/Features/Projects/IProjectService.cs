using TaskOps.Application.Shared.Api;

namespace TaskOps.Application.Features.Projects;

public interface IProjectService
{
    Task<ServiceResult<PagedResponse<ProjectListItemResponse>, ProjectFailure>> ListProjectsAsync(
        Guid organizationId,
        PageRequest page,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<ServiceResult<ProjectResponse, ProjectFailure>> CreateProjectAsync(
        Guid organizationId,
        CreateProjectRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ProjectResponse, ProjectFailure>> GetProjectAsync(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ProjectResponse, ProjectFailure>> UpdateProjectAsync(
        Guid organizationId,
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<object, ProjectFailure>> ArchiveProjectAsync(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken);
}
