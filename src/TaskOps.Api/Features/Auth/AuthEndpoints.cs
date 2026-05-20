using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Api;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register");

        group.MapPost("/login", LoginAsync)
            .WithName("Login");

        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshToken");

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout");

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        TaskOpsDbContext dbContext,
        PasswordHasher<User> passwordHasher,
        ITokenService tokenService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRegistration(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var emailExists = await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return Results.Conflict(new { message = "A user with this email already exists." });
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
        var response = CreateTokenPair(user, tokenService, dbContext);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/auth/users/{user.Id}", ApiResponse.Success(response, httpContext.TraceIdentifier));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        TaskOpsDbContext dbContext,
        PasswordHasher<User> passwordHasher,
        ITokenService tokenService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateLogin(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await dbContext.Users.FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user?.PasswordHash is null)
        {
            return InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        var response = CreateTokenPair(user, tokenService, dbContext);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(ApiResponse.Success(response, httpContext.TraceIdentifier));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        TaskOpsDbContext dbContext,
        ITokenService tokenService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["refreshToken"] = ["Refresh token is required."]
            });
        }

        var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var existingRefreshToken = await dbContext.RefreshTokens
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == refreshTokenHash, cancellationToken);

        if (existingRefreshToken is null ||
            existingRefreshToken.RevokedAt is not null ||
            existingRefreshToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Results.Unauthorized();
        }

        var newRefreshToken = tokenService.CreateRefreshToken();
        existingRefreshToken.RevokedAt = DateTimeOffset.UtcNow;
        existingRefreshToken.ReplacedByTokenHash = newRefreshToken.TokenHash;

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingRefreshToken.UserId,
            TokenHash = newRefreshToken.TokenHash,
            ExpiresAt = newRefreshToken.ExpiresAt
        });

        var accessToken = tokenService.CreateAccessToken(existingRefreshToken.User);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToAuthResponse(existingRefreshToken.User, accessToken, newRefreshToken);
        return Results.Ok(ApiResponse.Success(response, httpContext.TraceIdentifier));
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        ICurrentUserService currentUser,
        TaskOpsDbContext dbContext,
        ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["refreshToken"] = ["Refresh token is required."]
            });
        }

        var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var refreshToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(
            token => token.UserId == userId && token.TokenHash == refreshTokenHash,
            cancellationToken);

        if (refreshToken is { RevokedAt: null })
        {
            refreshToken.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ICurrentUserService currentUser,
        TaskOpsDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new CurrentUserResponse(user.Id, user.Email, user.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? Results.NotFound()
            : Results.Ok(ApiResponse.Success(user, httpContext.TraceIdentifier));
    }

    private static AuthResponse CreateTokenPair(User user, ITokenService tokenService, TaskOpsDbContext dbContext)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = tokenService.CreateRefreshToken();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            ExpiresAt = refreshToken.ExpiresAt
        });

        return ToAuthResponse(user, accessToken, refreshToken);
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

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static IResult InvalidCredentials()
    {
        return Results.Problem(
            title: "Invalid credentials.",
            statusCode: StatusCodes.Status401Unauthorized);
    }
}
