namespace TaskOps.Domain.Entities;

public sealed class Organization : AuditableEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    public ICollection<OrganizationMember> Members { get; set; } = [];

    public ICollection<Project> Projects { get; set; } = [];
}
