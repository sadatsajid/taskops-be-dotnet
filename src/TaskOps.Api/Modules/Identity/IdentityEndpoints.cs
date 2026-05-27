using TaskOps.Application.Modules.Identity;
using TaskOps.Application.SharedKernel.Api;
using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Modules.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
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

        return EndpointResults.CreatedOrFailure(
            result,
            AuthFailure.None,
            auth => $"/api/auth/users/{auth.CurrentUser.Id}",
            httpContext,
            ToFailureResult);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);

        return EndpointResults.OkOrFailure(result, AuthFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, cancellationToken);

        return EndpointResults.OkOrFailure(result, AuthFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(request, cancellationToken);

        return EndpointResults.NoContentOrFailure(result, AuthFailure.None, ToFailureResult);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authService.GetCurrentUserAsync(cancellationToken);

        return EndpointResults.OkOrFailure(result, AuthFailure.None, httpContext, ToFailureResult);
    }

    private static IResult ToFailureResult<T>(ServiceResult<T, AuthFailure> result)
    {
        return result.Failure switch
        {
            AuthFailure.Validation => EndpointResults.ValidationProblem(result.Errors),
            AuthFailure.DuplicateEmail => EndpointResults.ConflictProblem(
                "Duplicate email.",
                "A user with this email already exists."),
            AuthFailure.InvalidCredentials => EndpointResults.UnauthorizedProblem("Invalid credentials."),
            AuthFailure.Unauthorized => EndpointResults.Unauthorized(),
            AuthFailure.NotFound => EndpointResults.NotFound(),
            _ => EndpointResults.InternalServerError()
        };
    }
}
