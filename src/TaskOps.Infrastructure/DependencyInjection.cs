using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskOps.Application.Modules.Identity;
using TaskOps.Application.Modules.Issues;
using TaskOps.Application.Modules.Organizations;
using TaskOps.Application.Modules.Organizations.Access;
using TaskOps.Application.Modules.Projects;
using TaskOps.Domain.Modules.Identity;
using TaskOps.Infrastructure.Modules.Identity;
using TaskOps.Infrastructure.Modules.Issues;
using TaskOps.Infrastructure.Modules.Organizations;
using TaskOps.Infrastructure.Modules.Organizations.Access;
using TaskOps.Infrastructure.Modules.Projects;
using TaskOps.Infrastructure.Persistence;
using TaskOps.Infrastructure.Security;

namespace TaskOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskOpsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTaskOpsPersistence(configuration);

        services.AddScoped<IOrganizationAccessService, OrganizationAccessService>();
        services.AddSingleton<IJwtSigningKeyProvider, JwtSigningKeyProvider>();

        services.AddAuthFeature();
        services.AddOrganizationFeature();
        services.AddProjectFeature();
        services.AddIssueFeature();

        return services;
    }

    private static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<RefreshTokenRequest>, RefreshTokenRequestValidator>();
        services.AddScoped<IValidator<LogoutRequest>, LogoutRequestValidator>();

        return services;
    }

    private static IServiceCollection AddOrganizationFeature(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IValidator<CreateOrganizationRequest>, CreateOrganizationRequestValidator>();
        services.AddScoped<IValidator<UpdateOrganizationRequest>, UpdateOrganizationRequestValidator>();
        services.AddScoped<IValidator<AddOrganizationMemberRequest>, AddOrganizationMemberRequestValidator>();
        services.AddScoped<IValidator<ChangeOrganizationMemberRoleRequest>, ChangeOrganizationMemberRoleRequestValidator>();

        return services;
    }

    private static IServiceCollection AddProjectFeature(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IValidator<CreateProjectRequest>, CreateProjectRequestValidator>();
        services.AddScoped<IValidator<UpdateProjectRequest>, UpdateProjectRequestValidator>();

        return services;
    }

    private static IServiceCollection AddIssueFeature(this IServiceCollection services)
    {
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IValidator<IssueListQuery>, IssueListQueryValidator>();
        services.AddScoped<IValidator<CreateIssueRequest>, CreateIssueRequestValidator>();
        services.AddScoped<IValidator<UpdateIssueRequest>, UpdateIssueRequestValidator>();
        services.AddScoped<IValidator<ChangeIssueStatusRequest>, ChangeIssueStatusRequestValidator>();
        services.AddScoped<IValidator<ChangeIssuePriorityRequest>, ChangeIssuePriorityRequestValidator>();

        return services;
    }
}
