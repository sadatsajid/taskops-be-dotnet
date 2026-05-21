namespace TaskOps.Api.Features.Organizations;

public static class OrganizationDependencyInjection
{
    public static IServiceCollection AddOrganizationFeature(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();

        return services;
    }
}
