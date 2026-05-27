using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using TaskOps.Application.Modules.Identity;
using TaskOps.Domain.Modules.Identity;
using TaskOps.Infrastructure.Security;

namespace TaskOps.Infrastructure.Modules.Identity;

public sealed class TokenService(
    IOptions<JwtOptions> options,
    IJwtSigningKeyProvider signingKeyProvider,
    TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessTokenResult CreateAccessToken(User user)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new Claim[]
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingKeyProvider.CreateSigningCredentials());

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public RefreshTokenResult CreateRefreshToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = WebEncoders.Base64UrlEncode(tokenBytes);

        return new RefreshTokenResult(
            token,
            HashRefreshToken(token),
            timeProvider.GetUtcNow().AddDays(_options.RefreshTokenDays));
    }

    public string HashRefreshToken(string refreshToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(hash);
    }
}
