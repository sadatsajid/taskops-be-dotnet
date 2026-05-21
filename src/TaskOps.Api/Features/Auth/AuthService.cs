using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Api;
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
    private const int MaxDisplayNameLength = 120;
    private const int MinPasswordLength = 8;

    public async Task<ServiceResult<AuthResponse, AuthFailure>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRegistration(request);
        if (errors.Count > 0)
        {
            return ServiceResult<AuthResponse, AuthFailure>.Validation(AuthFailure.Validation, errors);
        }

        var normalizedEmail = EmailRules.Normalize(request.Email);
        var emailExists = await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return ServiceResult<AuthResponse, AuthFailure>.Failed(AuthFailure.DuplicateEmail);
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
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "IX_Users_NormalizedEmail"))
        {
            return ServiceResult<AuthResponse, AuthFailure>.Failed(AuthFailure.DuplicateEmail);
        }

        return ServiceResult<AuthResponse, AuthFailure>.Success(response, AuthFailure.None);
    }

    public async Task<ServiceResult<AuthResponse, AuthFailure>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateLogin(request);
        if (errors.Count > 0)
        {
            return ServiceResult<AuthResponse, AuthFailure>.Validation(AuthFailure.Validation, errors);
        }

        var normalizedEmail = EmailRules.Normalize(request.Email);
        var user = await dbContext.Users.FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user?.PasswordHash is null)
        {
            return ServiceResult<AuthResponse, AuthFailure>.Failed(AuthFailure.InvalidCredentials);
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return ServiceResult<AuthResponse, AuthFailure>.Failed(AuthFailure.InvalidCredentials);
        }

        var response = CreateTokenPair(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AuthResponse, AuthFailure>.Success(response, AuthFailure.None);
    }

    public async Task<ServiceResult<AuthResponse, AuthFailure>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ServiceResult<AuthResponse, AuthFailure>.Validation(AuthFailure.Validation, RefreshTokenRequired());
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
            return ServiceResult<AuthResponse, AuthFailure>.Failed(AuthFailure.Unauthorized);
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
            return ServiceResult<AuthResponse, AuthFailure>.Failed(AuthFailure.Unauthorized);
        }

        AddRefreshToken(existingRefreshToken.UserId, newRefreshToken);

        var accessToken = tokenService.CreateAccessToken(existingRefreshToken.User);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<AuthResponse, AuthFailure>.Success(
            ToAuthResponse(existingRefreshToken.User, accessToken, newRefreshToken),
            AuthFailure.None);
    }

    public async Task<ServiceResult<object, AuthFailure>> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return ServiceResult<object, AuthFailure>.Failed(AuthFailure.Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ServiceResult<object, AuthFailure>.Validation(AuthFailure.Validation, RefreshTokenRequired());
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

        return ServiceResult<object, AuthFailure>.Success(Empty, AuthFailure.None);
    }

    public async Task<ServiceResult<CurrentUserResponse, AuthFailure>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return ServiceResult<CurrentUserResponse, AuthFailure>.Failed(AuthFailure.Unauthorized);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new CurrentUserResponse(user.Id, user.Email, user.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? ServiceResult<CurrentUserResponse, AuthFailure>.Failed(AuthFailure.NotFound)
            : ServiceResult<CurrentUserResponse, AuthFailure>.Success(user, AuthFailure.None);
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
        var errors = EmailRules.Validate(email);

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
}
