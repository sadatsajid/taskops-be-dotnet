using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    private const int MaxEmailLength = 320;
    private const int MaxDisplayNameLength = 120;
    private const int MinPasswordLength = 8;

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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Users_NormalizedEmail"))
        {
            return AuthServiceResult<AuthResponse>.Failed(AuthFailure.DuplicateEmail);
        }

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
        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingRefreshToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == refreshTokenHash, cancellationToken);

        if (existingRefreshToken is null ||
            existingRefreshToken.RevokedAt is not null ||
            existingRefreshToken.ExpiresAt <= now)
        {
            return AuthServiceResult<AuthResponse>.Failed(AuthFailure.Unauthorized);
        }

        var newRefreshToken = tokenService.CreateRefreshToken();
        var revokedCount = await dbContext.RefreshTokens
            .Where(refreshToken =>
                refreshToken.Id == existingRefreshToken.Id &&
                refreshToken.RevokedAt == null &&
                refreshToken.ExpiresAt > now)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(refreshToken => refreshToken.RevokedAt, now)
                .SetProperty(refreshToken => refreshToken.ReplacedByTokenHash, newRefreshToken.TokenHash), cancellationToken);

        if (revokedCount != 1)
        {
            return AuthServiceResult<AuthResponse>.Failed(AuthFailure.Unauthorized);
        }

        AddRefreshToken(existingRefreshToken.UserId, newRefreshToken);

        var accessToken = tokenService.CreateAccessToken(existingRefreshToken.User);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
        var errors = ValidateLoginShape(request.Email, request.Password);

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < MinPasswordLength)
        {
            errors["password"] = [$"Password must be at least {MinPasswordLength} characters."];
        }

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0 || displayName.Length > MaxDisplayNameLength)
        {
            errors["displayName"] = [$"Display name must be between 1 and {MaxDisplayNameLength} characters."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateLogin(LoginRequest request) =>
        ValidateLoginShape(request.Email, request.Password);

    private static Dictionary<string, string[]> ValidateLoginShape(string? email, string? password)
    {
        var errors = new Dictionary<string, string[]>();
        var trimmedEmail = email?.Trim() ?? string.Empty;

        if (trimmedEmail.Length == 0 ||
            trimmedEmail.Length > MaxEmailLength ||
            !trimmedEmail.Contains('@', StringComparison.Ordinal))
        {
            errors["email"] = [$"A valid email up to {MaxEmailLength} characters is required."];
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors["password"] = ["Password is required."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> RefreshTokenRequired() => new()
    {
        ["refreshToken"] = ["Refresh token is required."]
    };

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: var actualConstraintName
        } && actualConstraintName == constraintName;
}
