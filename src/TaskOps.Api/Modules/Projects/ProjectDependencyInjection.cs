using FluentValidation;

namespace TaskOps.Api.Modules.Projects;

public static class ProjectDependencyInjection
{
    public static IServiceCollection AddProjectModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IValidator<CreateProjectRequest>, CreateProjectRequestValidator>();
        services.AddScoped<IValidator<UpdateProjectRequest>, UpdateProjectRequestValidator>();

        return services;
    }
}
