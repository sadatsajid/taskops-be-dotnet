namespace TaskOps.Application.Modules.Projects;

public sealed record CreateProjectRequest(
    string Name,
    string Key,
    string? Description);

public sealed record UpdateProjectRequest(
    string Name,
    string Key,
    string? Description);

public sealed record ProjectListItemResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Key,
    bool IsArchived);

public sealed record ProjectResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Key,
    string? Description,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
