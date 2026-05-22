using FluentValidation;

namespace TaskOps.Api.Features.Organizations;

public static class OrganizationDependencyInjection
{
    public static IServiceCollection AddOrganizationFeature(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IValidator<CreateOrganizationRequest>, CreateOrganizationRequestValidator>();
        services.AddScoped<IValidator<UpdateOrganizationRequest>, UpdateOrganizationRequestValidator>();
        services.AddScoped<IValidator<AddOrganizationMemberRequest>, AddOrganizationMemberRequestValidator>();
        services.AddScoped<IValidator<ChangeOrganizationMemberRoleRequest>, ChangeOrganizationMemberRoleRequestValidator>();

        return services;
    }
}
