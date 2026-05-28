using TaskOps.Domain.Modules.Organizations;
using TaskOps.Domain.SharedKernel;

namespace TaskOps.Domain.Modules.Issues;

public sealed class IssueComment : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid IssueId { get; set; }

    public Issue Issue { get; set; } = null!;

    public Guid AuthorMemberId { get; set; }

    public OrganizationMember Author { get; set; } = null!;

    public required string Body { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedByMemberId { get; set; }

    public OrganizationMember? DeletedBy { get; set; }
}
