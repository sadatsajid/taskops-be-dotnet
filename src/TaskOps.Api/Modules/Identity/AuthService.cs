using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Api;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Modules.Identity;

public sealed class AuthService(
    TaskOpsDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshTokenRequest> refreshTokenValidator,
    IValidator<LogoutRequest> logoutValidator) : IAuthService
{
    private static readonly object Empty = new();

    public async Task<ServiceResult<AuthResponse, AuthFailure>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<AuthResponse, AuthFailure>.Validation(AuthFailure.Validation, validation.ToErrorDictionary());
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
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<AuthResponse, AuthFailure>.Validation(AuthFailure.Validation, validation.ToErrorDictionary());
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
        var validation = await refreshTokenValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<AuthResponse, AuthFailure>.Validation(AuthFailure.Validation, validation.ToErrorDictionary());
        }

        var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingRefreshToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == refreshTokenHash, cancellationToken);

        if (existingRefreshToken is null)
        {
            return ServiceResult<AuthResponse, AuthFailure>.Failed(AuthFailure.Unauthorized);
        }

        if (existingRefreshToken.RevokedAt is not null)
        {
            await RevokeDescendantRefreshTokensAsync(existingRefreshToken.UserId, existingRefreshToken.ReplacedByTokenHash, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<AuthResponse, AuthFailure>.Failed(AuthFailure.Unauthorized);
        }

        if (existingRefreshToken.ExpiresAt <= now)
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
        var validation = await logoutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<object, AuthFailure>.Validation(AuthFailure.Validation, validation.ToErrorDictionary());
        }

        var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var refreshToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(
            token => token.TokenHash == refreshTokenHash,
            cancellationToken);

        if (refreshToken is not null)
        {
            if (refreshToken.RevokedAt is null)
            {
                refreshToken.RevokedAt = now;
            }

            await RevokeDescendantRefreshTokensAsync(refreshToken.UserId, refreshToken.ReplacedByTokenHash, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
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

    private async Task RevokeDescendantRefreshTokensAsync(
        Guid userId,
        string? replacedByTokenHash,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        var nextTokenHash = replacedByTokenHash;

        while (!string.IsNullOrWhiteSpace(nextTokenHash))
        {
            var descendantToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(
                token => token.UserId == userId && token.TokenHash == nextTokenHash,
                cancellationToken);
            if (descendantToken is null)
            {
                return;
            }

            if (descendantToken.RevokedAt is null)
            {
                descendantToken.RevokedAt = revokedAt;
            }

            nextTokenHash = descendantToken.ReplacedByTokenHash;
        }
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

}
