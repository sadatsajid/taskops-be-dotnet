using TaskOps.Api.Modules.Organizations;

namespace TaskOps.Api.Modules;

public static class ModuleDependencyInjection
{
    public static IServiceCollection AddTaskOpsModules(this IServiceCollection services)
    {
        services.AddOrganizationModule();

        return services;
    }
}
