using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using TaskOps.Api.Modules.Organizations.Access;
using TaskOps.Application.Modules.Organizations.Access;
using TaskOps.Domain.Modules.Organizations;

namespace TaskOps.Api.Modules.Organizations;

public static class OrganizationDependencyInjection
{
    public static IServiceCollection AddOrganizationModule(this IServiceCollection services)
    {
        services.AddScoped<OrganizationContext>();
        services.AddScoped<IOrganizationContext>(sp => sp.GetRequiredService<OrganizationContext>());
        services.AddScoped<IOrganizationContextAccessor>(sp => sp.GetRequiredService<OrganizationContext>());
        services.AddScoped<IAuthorizationHandler, OrganizationMembershipHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, OrganizationAuthorizationResultHandler>();

        services.Configure<AuthorizationOptions>(options =>
        {
            options.AddPolicy(OrganizationPolicies.Member, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new OrganizationMembershipRequirement()));

            options.AddPolicy(OrganizationPolicies.Owner, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new OrganizationMembershipRequirement(OrganizationRolePolicies.OwnerOnly)));

            options.AddPolicy(OrganizationPolicies.ProjectManagement, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new OrganizationMembershipRequirement(OrganizationRolePolicies.ProjectManagement)));
        });

        return services;
    }
}
