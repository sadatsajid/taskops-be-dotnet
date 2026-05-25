using TaskOps.Api.Modules.Identity;
using TaskOps.Api.Modules.Issues;
using TaskOps.Api.Modules.Organizations;
using TaskOps.Api.Modules.Projects;
using TaskOps.Api.Modules.System;

namespace TaskOps.Api.Modules;

public static class ModuleEndpoints
{
    public static IEndpointRouteBuilder MapTaskOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSystemEndpoints();
        endpoints.MapIdentityEndpoints();
        endpoints.MapOrganizationEndpoints();
        endpoints.MapProjectEndpoints();
        endpoints.MapIssueEndpoints();

        return endpoints;
    }
}
