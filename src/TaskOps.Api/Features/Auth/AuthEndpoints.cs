using TaskOps.Api.Shared.Api;

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
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);

        return result.IsSuccess(AuthFailure.None)
            ? EndpointResults.Created($"/api/auth/users/{result.Value!.CurrentUser.Id}", result.Value, httpContext)
            : ToFailureResult(result);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);

        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, cancellationToken);

        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(request, cancellationToken);

        return result.Failure == AuthFailure.None
            ? Results.NoContent()
            : ToFailureResult(result);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authService.GetCurrentUserAsync(cancellationToken);

        return ToOkResult(result, httpContext);
    }

    private static IResult ToOkResult<T>(ServiceResult<T, AuthFailure> result, HttpContext httpContext)
    {
        return result.IsSuccess(AuthFailure.None)
            ? EndpointResults.Ok(result.Value!, httpContext)
            : ToFailureResult(result);
    }

    private static IResult ToFailureResult<T>(ServiceResult<T, AuthFailure> result)
    {
        return result.Failure switch
        {
            AuthFailure.Validation => EndpointResults.ValidationProblem(result.Errors),
            AuthFailure.DuplicateEmail => Results.Problem(
                title: "Duplicate email.",
                detail: "A user with this email already exists.",
                statusCode: StatusCodes.Status409Conflict),
            AuthFailure.InvalidCredentials => Results.Problem(
                title: "Invalid credentials.",
                statusCode: StatusCodes.Status401Unauthorized),
            AuthFailure.Unauthorized => Results.Unauthorized(),
            AuthFailure.NotFound => Results.NotFound(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
