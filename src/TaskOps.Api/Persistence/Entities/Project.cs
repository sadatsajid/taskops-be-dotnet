namespace TaskOps.Api.Persistence.Entities;

public sealed class Project : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } = null!;

    public required string Name { get; set; }

    public required string Key { get; set; }

    public string? Description { get; set; }

    public bool IsArchived { get; set; }

    public ICollection<Issue> Issues { get; set; } = [];
}
