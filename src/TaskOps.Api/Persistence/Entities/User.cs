namespace TaskOps.Api.Persistence.Entities;

public sealed class User : AuditableEntity
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public required string DisplayName { get; set; }

    public string? PasswordHash { get; set; }

    public ICollection<OrganizationMember> OrganizationMemberships { get; set; } = [];
}
