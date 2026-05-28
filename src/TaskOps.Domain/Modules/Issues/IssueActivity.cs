using TaskOps.Domain.Modules.Organizations;

namespace TaskOps.Domain.Modules.Issues;

public sealed class IssueActivity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid IssueId { get; set; }

    public Issue Issue { get; set; } = null!;

    public Guid? ActorMemberId { get; set; }

    public OrganizationMember? Actor { get; set; }

    public IssueActivityType Type { get; set; }

    public string? Field { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public Guid? CommentId { get; set; }

    public IssueComment? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
