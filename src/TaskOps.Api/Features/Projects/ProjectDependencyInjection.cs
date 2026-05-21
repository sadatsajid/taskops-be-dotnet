namespace TaskOps.Api.Features.Projects;

public static class ProjectDependencyInjection
{
    public static IServiceCollection AddProjectFeature(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();

        return services;
    }
}
