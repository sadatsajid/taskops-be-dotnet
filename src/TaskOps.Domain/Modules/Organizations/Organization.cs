using TaskOps.Domain.Modules.Projects;
using TaskOps.Domain.SharedKernel;

namespace TaskOps.Domain.Modules.Organizations;

public sealed class Organization : AuditableEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    public ICollection<OrganizationMember> Members { get; set; } = [];

    public ICollection<Project> Projects { get; set; } = [];
}
