using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Api;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Features.Projects;

public sealed class ProjectService(
    TaskOpsDbContext dbContext,
    IOrganizationAccessService organizationAccess,
    TimeProvider timeProvider) : IProjectService
{
    private static readonly object Empty = new();
    private static readonly OrganizationRole[] ProjectManagers =
    [
        OrganizationRole.Owner,
        OrganizationRole.Admin,
        OrganizationRole.ProjectManager
    ];
    private const int MaxNameLength = 160;
    private const int MaxKeyLength = 20;
    private const int MaxDescriptionLength = 2000;

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

        var errors = ValidateProject(request.Name, request.Key, request.Description);
        if (errors.Count > 0)
        {
            return Validation<ProjectResponse>(errors);
        }

        var normalizedKey = NormalizeKey(request.Key);
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
            Description = NormalizeDescription(request.Description),
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

        var errors = ValidateProject(request.Name, request.Key, request.Description);
        if (errors.Count > 0)
        {
            return Validation<ProjectResponse>(errors);
        }

        var project = await dbContext.Projects.FirstOrDefaultAsync(
            project => project.OrganizationId == organizationId && project.Id == projectId,
            cancellationToken);
        if (project is null)
        {
            return Failure<ProjectResponse>(ProjectFailure.NotFound);
        }

        var normalizedKey = NormalizeKey(request.Key);
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
        project.Description = NormalizeDescription(request.Description);

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
        var access = await organizationAccess.RequireAnyRoleAsync(organizationId, ProjectManagers, cancellationToken);
        return access.IsAllowed ? ProjectFailure.None : ToProjectFailure(access.Status);
    }

    private static Dictionary<string, string[]> ValidateProject(string? name, string? key, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        var trimmedName = name?.Trim() ?? string.Empty;
        var normalizedKey = NormalizeKey(key ?? string.Empty);
        var trimmedDescription = description?.Trim();

        if (trimmedName.Length == 0 || trimmedName.Length > MaxNameLength)
        {
            errors["name"] = [$"Name must be between 1 and {MaxNameLength} characters."];
        }

        if (!IsValidKey(normalizedKey))
        {
            errors["key"] = [$"Key must be between 1 and {MaxKeyLength} characters and contain only uppercase letters, numbers, and hyphens."];
        }

        if (trimmedDescription?.Length > MaxDescriptionLength)
        {
            errors["description"] = [$"Description must be {MaxDescriptionLength} characters or fewer."];
        }

        return errors;
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length == 0 || key.Length > MaxKeyLength)
        {
            return false;
        }

        if (!char.IsLetterOrDigit(key[0]) || !char.IsLetterOrDigit(key[^1]))
        {
            return false;
        }

        return key.All(character =>
            character is '-' ||
            character is >= 'A' and <= 'Z' ||
            character is >= '0' and <= '9');
    }

    private static string NormalizeKey(string key) => key.Trim().ToUpperInvariant();

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
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
