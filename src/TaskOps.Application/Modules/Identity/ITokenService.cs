using TaskOps.Domain.Modules.Identity;

namespace TaskOps.Application.Modules.Identity;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user);

    RefreshTokenResult CreateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
