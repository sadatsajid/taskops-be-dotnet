using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TaskOps.Application.Features.Projects;
using TaskOps.Application.Shared.Api;
using TaskOps.Application.Shared.Security;
using TaskOps.Domain.Entities;
using TaskOps.Domain.Security;
using TaskOps.Infrastructure.Persistence;

namespace TaskOps.Infrastructure.Features.Projects;

public sealed class ProjectService(
    TaskOpsDbContext dbContext,
    IOrganizationAccessService organizationAccess,
    TimeProvider timeProvider,
    IValidator<CreateProjectRequest> createProjectValidator,
    IValidator<UpdateProjectRequest> updateProjectValidator) : IProjectService
{
    private static readonly object Empty = new();

    public async Task<ServiceResult<PagedResponse<ProjectListItemResponse>, ProjectFailure>> ListProjectsAsync(
        Guid organizationId,
        PageRequest page,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var access = await organizationAccess.RequireMembershipAsync(organizationId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Failure<PagedResponse<ProjectListItemResponse>>(ToProjectFailure(access.Status));
        }

        var limit = page.SafeLimit;
        var offset = page.SafeOffset;
        var projects = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OrganizationId == organizationId)
            .Where(project => includeArchived || !project.IsArchived)
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .Skip(offset)
            .Take(limit + 1)
            .Select(project => new ProjectListItemResponse(
                project.Id,
                project.OrganizationId,
                project.Name,
                project.Key,
                project.IsArchived))
            .ToListAsync(cancellationToken);

        return Success(
            new PagedResponse<ProjectListItemResponse>(
                projects.Take(limit).ToList(),
                offset,
                limit,
                projects.Count > limit));
    }

    public async Task<ServiceResult<ProjectResponse, ProjectFailure>> CreateProjectAsync(
        Guid organizationId,
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagerAsync(organizationId, cancellationToken);
        if (access != ProjectFailure.None)
        {
            return Failure<ProjectResponse>(access);
        }

        var validation = await createProjectValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<ProjectResponse>(validation.ToErrorDictionary());
        }

        var normalizedKey = ProjectValidation.NormalizeKey(request.Key);
        var keyTaken = await dbContext.Projects.AnyAsync(
            project => project.OrganizationId == organizationId && project.Key == normalizedKey,
            cancellationToken);
        if (keyTaken)
        {
            return Failure<ProjectResponse>(ProjectFailure.DuplicateKey);
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = request.Name.Trim(),
            Key = normalizedKey,
            Description = ProjectValidation.NormalizeDescription(request.Description),
            IsArchived = false
        };

        dbContext.Projects.Add(project);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "IX_Projects_OrganizationId_Key"))
        {
            return Failure<ProjectResponse>(ProjectFailure.DuplicateKey);
        }

        return Success(ToResponse(project));
    }

    public async Task<ServiceResult<ProjectResponse, ProjectFailure>> GetProjectAsync(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var access = await organizationAccess.RequireMembershipAsync(organizationId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Failure<ProjectResponse>(ToProjectFailure(access.Status));
        }

        var response = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OrganizationId == organizationId && project.Id == projectId)
            .Select(project => new ProjectResponse(
                project.Id,
                project.OrganizationId,
                project.Name,
                project.Key,
                project.Description,
                project.IsArchived,
                project.CreatedAt,
                project.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Failure<ProjectResponse>(ProjectFailure.NotFound)
            : Success(response);
    }

    public async Task<ServiceResult<ProjectResponse, ProjectFailure>> UpdateProjectAsync(
        Guid organizationId,
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagerAsync(organizationId, cancellationToken);
        if (access != ProjectFailure.None)
        {
            return Failure<ProjectResponse>(access);
        }

        var validation = await updateProjectValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<ProjectResponse>(validation.ToErrorDictionary());
        }

        var project = await dbContext.Projects.FirstOrDefaultAsync(
            project => project.OrganizationId == organizationId && project.Id == projectId,
            cancellationToken);
        if (project is null)
        {
            return Failure<ProjectResponse>(ProjectFailure.NotFound);
        }

        var normalizedKey = ProjectValidation.NormalizeKey(request.Key);
        var keyTaken = await dbContext.Projects.AnyAsync(
            project =>
                project.OrganizationId == organizationId &&
                project.Id != projectId &&
                project.Key == normalizedKey,
            cancellationToken);
        if (keyTaken)
        {
            return Failure<ProjectResponse>(ProjectFailure.DuplicateKey);
        }

        project.Name = request.Name.Trim();
        project.Key = normalizedKey;
        project.Description = ProjectValidation.NormalizeDescription(request.Description);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "IX_Projects_OrganizationId_Key"))
        {
            return Failure<ProjectResponse>(ProjectFailure.DuplicateKey);
        }

        return Success(ToResponse(project));
    }

    public async Task<ServiceResult<object, ProjectFailure>> ArchiveProjectAsync(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagerAsync(organizationId, cancellationToken);
        if (access != ProjectFailure.None)
        {
            return Failure<object>(access);
        }

        var updated = await dbContext.Projects
            .Where(project => project.OrganizationId == organizationId && project.Id == projectId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(project => project.IsArchived, true)
                    .SetProperty(project => project.UpdatedAt, timeProvider.GetUtcNow()),
                cancellationToken);

        return updated == 0
            ? Failure<object>(ProjectFailure.NotFound)
            : Success(Empty);
    }

    private async Task<ProjectFailure> RequireProjectManagerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var access = await organizationAccess.RequireAnyRoleAsync(
            organizationId,
            OrganizationRolePolicies.ProjectManagement,
            cancellationToken);
        return access.IsAllowed ? ProjectFailure.None : ToProjectFailure(access.Status);
    }

    private static ProjectResponse ToResponse(Project project) =>
        new(
            project.Id,
            project.OrganizationId,
            project.Name,
            project.Key,
            project.Description,
            project.IsArchived,
            project.CreatedAt,
            project.UpdatedAt);

    private static ProjectFailure ToProjectFailure(OrganizationAccessStatus status)
    {
        return status switch
        {
            OrganizationAccessStatus.Unauthorized => ProjectFailure.Unauthorized,
            OrganizationAccessStatus.NotFound => ProjectFailure.NotFound,
            OrganizationAccessStatus.Forbidden => ProjectFailure.Forbidden,
            _ => ProjectFailure.None
        };
    }

    private static ServiceResult<T, ProjectFailure> Success<T>(T value) =>
        ServiceResult<T, ProjectFailure>.Success(value, ProjectFailure.None);

    private static ServiceResult<T, ProjectFailure> Validation<T>(IReadOnlyDictionary<string, string[]> errors) =>
        ServiceResult<T, ProjectFailure>.Validation(ProjectFailure.Validation, errors);

    private static ServiceResult<T, ProjectFailure> Failure<T>(ProjectFailure failure) =>
        ServiceResult<T, ProjectFailure>.Failed(failure);
}
