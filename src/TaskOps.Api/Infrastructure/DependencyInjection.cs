using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using TaskOps.Api.Features.Auth;
using TaskOps.Api.Features.Organizations;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskOpsApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IJwtSigningKeyProvider, JwtSigningKeyProvider>();
        services.ConfigureOptions<JwtBearerOptionsSetup>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IOrganizationAccessService, OrganizationAccessService>();
        services.AddAuthFeature();
        services.AddOrganizationFeature();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length >= 32,
                "Jwt:SigningKey must be configured with at least 32 characters.")
            .Validate(options => options.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be greater than zero.")
            .Validate(options => options.RefreshTokenDays > 0, "Jwt:RefreshTokenDays must be greater than zero.")
            .ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddAuthorization();

        return services;
    }

    public static WebApplication UseTaskOpsApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var httpContext = statusCodeContext.HttpContext;
            var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
            var statusCode = httpContext.Response.StatusCode;

            await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Title = ReasonPhrases.GetReasonPhrase(statusCode),
                    Status = statusCode,
                    Instance = httpContext.Request.Path
                }
            });
        });
        app.UseMiddleware<RequestLoggingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapSwaggerUi();
        }

        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
