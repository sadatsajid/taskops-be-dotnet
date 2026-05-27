namespace TaskOps.Application.Shared.Security;

public interface ICurrentUserService
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}
