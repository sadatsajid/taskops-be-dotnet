namespace TaskOps.Api.Infrastructure;

public static class OpenApiDependencyInjection
{
    public static IServiceCollection AddTaskOpsOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi();

        return services;
    }
}
