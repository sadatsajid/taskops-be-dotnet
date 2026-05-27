using TaskOps.Domain.SharedKernel;

namespace TaskOps.Domain.Modules.Identity;

public sealed class RefreshToken : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }
}
