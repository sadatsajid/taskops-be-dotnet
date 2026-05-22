using FluentValidation;

namespace TaskOps.Api.Features.Projects;

public static class ProjectDependencyInjection
{
    public static IServiceCollection AddProjectFeature(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IValidator<CreateProjectRequest>, CreateProjectRequestValidator>();
        services.AddScoped<IValidator<UpdateProjectRequest>, UpdateProjectRequestValidator>();

        return services;
    }
}
