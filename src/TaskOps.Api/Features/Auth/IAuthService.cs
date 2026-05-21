namespace TaskOps.Api.Features.Auth;

public interface IAuthService
{
    Task<AuthServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<object>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken);
}
