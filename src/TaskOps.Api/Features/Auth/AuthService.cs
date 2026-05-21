using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Features.Auth;

public sealed class AuthService(
    TaskOpsDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IAuthService
{
    private static readonly object Empty = new();

    public async Task<AuthServiceResult<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRegistration(request);
        if (errors.Count > 0)
        {
            return AuthServiceResult<AuthResponse>.Validation(errors);
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var emailExists = await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return AuthServiceResult<AuthResponse>.Failed(AuthFailure.DuplicateEmail);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        var response = CreateTokenPair(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<AuthResponse>.Success(response);
    }

    public async Task<AuthServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateLogin(request);
        if (errors.Count > 0)
        {
            return AuthServiceResult<AuthResponse>.Validation(errors);
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await dbContext.Users.FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user?.PasswordHash is null)
        {
            return AuthServiceResult<AuthResponse>.Failed(AuthFailure.InvalidCredentials);
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return AuthServiceResult<AuthResponse>.Failed(AuthFailure.InvalidCredentials);
        }

        var response = CreateTokenPair(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<AuthResponse>.Success(response);
    }

    public async Task<AuthServiceResult<AuthResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return AuthServiceResult<AuthResponse>.Validation(RefreshTokenRequired());
        }

        var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var existingRefreshToken = await dbContext.RefreshTokens
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == refreshTokenHash, cancellationToken);

        if (existingRefreshToken is null ||
            existingRefreshToken.RevokedAt is not null ||
            existingRefreshToken.ExpiresAt <= timeProvider.GetUtcNow())
        {
            return AuthServiceResult<AuthResponse>.Failed(AuthFailure.Unauthorized);
        }

        var newRefreshToken = tokenService.CreateRefreshToken();
        existingRefreshToken.RevokedAt = timeProvider.GetUtcNow();
        existingRefreshToken.ReplacedByTokenHash = newRefreshToken.TokenHash;

        AddRefreshToken(existingRefreshToken.UserId, newRefreshToken);

        var accessToken = tokenService.CreateAccessToken(existingRefreshToken.User);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<AuthResponse>.Success(ToAuthResponse(existingRefreshToken.User, accessToken, newRefreshToken));
    }

    public async Task<AuthServiceResult<object>> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return AuthServiceResult<object>.Failed(AuthFailure.Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return AuthServiceResult<object>.Validation(RefreshTokenRequired());
        }

        var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var refreshToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(
            token => token.UserId == userId && token.TokenHash == refreshTokenHash,
            cancellationToken);

        if (refreshToken is { RevokedAt: null })
        {
            refreshToken.RevokedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return AuthServiceResult<object>.Success(Empty);
    }

    public async Task<AuthServiceResult<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return AuthServiceResult<CurrentUserResponse>.Failed(AuthFailure.Unauthorized);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new CurrentUserResponse(user.Id, user.Email, user.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? AuthServiceResult<CurrentUserResponse>.Failed(AuthFailure.NotFound)
            : AuthServiceResult<CurrentUserResponse>.Success(user);
    }

    private AuthResponse CreateTokenPair(User user)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = tokenService.CreateRefreshToken();

        AddRefreshToken(user.Id, refreshToken);

        return ToAuthResponse(user, accessToken, refreshToken);
    }

    private void AddRefreshToken(Guid userId, RefreshTokenResult refreshToken)
    {
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = refreshToken.TokenHash,
            ExpiresAt = refreshToken.ExpiresAt
        });
    }

    private static AuthResponse ToAuthResponse(User user, AccessTokenResult accessToken, RefreshTokenResult refreshToken)
    {
        return new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.Token,
            refreshToken.ExpiresAt,
            new CurrentUserResponse(user.Id, user.Email, user.DisplayName));
    }

    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest request)
    {
        var errors = ValidateLogin(new LoginRequest(request.Email, request.Password));

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["displayName"] = ["Display name is required."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateLogin(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@', StringComparison.Ordinal))
        {
            errors["email"] = ["A valid email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            errors["password"] = ["Password must be at least 8 characters."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> RefreshTokenRequired() => new()
    {
        ["refreshToken"] = ["Refresh token is required."]
    };

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
