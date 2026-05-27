namespace TaskOps.Application.SharedKernel.Security;

public interface ICurrentUserService
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}
