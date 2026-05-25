using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TaskOps.Api.Infrastructure;
using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Modules.System;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", (HttpContext httpContext) =>
            Results.Ok(ApiResponse.Success(new
            {
                service = "TaskOps.Api",
                message = "TaskOps API is running."
            }, httpContext.TraceIdentifier)))
            .WithName("Root")
            .WithTags("System");

        endpoints.MapGet("/api/status", (IHostEnvironment environment, TimeProvider timeProvider, HttpContext httpContext) =>
            Results.Ok(ApiResponse.Success(new
            {
                service = "TaskOps.Api",
                environment = environment.EnvironmentName,
                utcNow = timeProvider.GetUtcNow()
            }, httpContext.TraceIdentifier)))
            .WithName("GetApiStatus")
            .WithTags("System");

        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
        })
        .WithName("Health")
        .WithTags("System");

        return endpoints;
    }
}
