using TaskOps.Api.Features.Auth;
using TaskOps.Api.Features.Issues;
using TaskOps.Api.Features.Organizations;
using TaskOps.Api.Features.Projects;
using TaskOps.Api.Features.System;

namespace TaskOps.Api.Features;

public static class FeatureEndpoints
{
    public static IEndpointRouteBuilder MapTaskOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSystemEndpoints();
        endpoints.MapAuthEndpoints();
        endpoints.MapOrganizationEndpoints();
        endpoints.MapProjectEndpoints();
        endpoints.MapIssueEndpoints();

        return endpoints;
    }
}
