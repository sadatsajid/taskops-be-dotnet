namespace TaskOps.Domain.SharedKernel;

public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
