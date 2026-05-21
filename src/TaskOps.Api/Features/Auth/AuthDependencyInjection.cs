using Microsoft.AspNetCore.Identity;
using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Features.Auth;

public static class AuthDependencyInjection
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        return services;
    }
}
