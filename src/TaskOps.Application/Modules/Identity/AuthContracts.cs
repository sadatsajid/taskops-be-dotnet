namespace TaskOps.Application.Modules.Identity;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserResponse CurrentUser);
