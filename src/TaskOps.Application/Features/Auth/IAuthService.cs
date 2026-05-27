using TaskOps.Application.Shared.Api;

namespace TaskOps.Application.Features.Auth;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse, AuthFailure>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<AuthResponse, AuthFailure>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<AuthResponse, AuthFailure>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object, AuthFailure>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<CurrentUserResponse, AuthFailure>> GetCurrentUserAsync(CancellationToken cancellationToken);
}
