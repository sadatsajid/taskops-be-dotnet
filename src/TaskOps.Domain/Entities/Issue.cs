namespace TaskOps.Domain.Entities;

public sealed class Issue : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } = null!;

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public int Number { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public IssueStatus Status { get; set; }

    public IssuePriority Priority { get; set; }

    public Guid? AssigneeId { get; set; }

    public OrganizationMember? Assignee { get; set; }

    public DateOnly? DueDate { get; set; }
}
