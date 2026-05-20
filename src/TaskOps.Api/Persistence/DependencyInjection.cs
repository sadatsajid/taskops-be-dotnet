using Microsoft.EntityFrameworkCore;

namespace TaskOps.Api.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskOpsPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseConnectionString = configuration.GetConnectionString("TaskOpsDatabase");

        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.AddDbContext<TaskOpsDbContext>(options =>
        {
            options.UseNpgsql(databaseConnectionString);
        });
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgresql");

        return services;
    }
}
