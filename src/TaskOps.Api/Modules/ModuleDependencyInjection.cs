using TaskOps.Api.Modules.Identity;
using TaskOps.Api.Modules.Issues;
using TaskOps.Api.Modules.Organizations;
using TaskOps.Api.Modules.Projects;

namespace TaskOps.Api.Modules;

public static class ModuleDependencyInjection
{
    public static IServiceCollection AddTaskOpsModules(this IServiceCollection services)
    {
        services.AddIdentityModule();
        services.AddOrganizationModule();
        services.AddProjectModule();
        services.AddIssueModule();

        return services;
    }
}
