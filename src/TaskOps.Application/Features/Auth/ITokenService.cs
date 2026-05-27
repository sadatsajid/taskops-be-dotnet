using TaskOps.Domain.Entities;

namespace TaskOps.Application.Features.Auth;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user);

    RefreshTokenResult CreateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
