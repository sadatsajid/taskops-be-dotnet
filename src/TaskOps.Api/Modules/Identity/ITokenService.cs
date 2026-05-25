using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Modules.Identity;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user);

    RefreshTokenResult CreateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
