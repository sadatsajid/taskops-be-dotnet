using TaskOps.Domain.Modules.Identity;
using TaskOps.Domain.Modules.Issues;
using TaskOps.Domain.SharedKernel;

namespace TaskOps.Domain.Modules.Organizations;

public sealed class OrganizationMember : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public OrganizationRole Role { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public ICollection<Issue> AssignedIssues { get; set; } = [];
}
