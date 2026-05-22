using TaskOps.Api.Features.Auth;
using TaskOps.Api.Features.Issues;
using TaskOps.Api.Features.Organizations;
using TaskOps.Api.Features.Projects;

namespace TaskOps.Api.Features;

public static class FeatureDependencyInjection
{
    public static IServiceCollection AddTaskOpsFeatures(this IServiceCollection services)
    {
        services.AddAuthFeature();
        services.AddOrganizationFeature();
        services.AddProjectFeature();
        services.AddIssueFeature();

        return services;
    }
}
