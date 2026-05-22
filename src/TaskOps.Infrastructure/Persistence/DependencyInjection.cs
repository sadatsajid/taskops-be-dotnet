using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TaskOps.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskOpsPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("Database"))
            .ValidateOnStart();
        services.AddDbContext<TaskOpsDbContext>((serviceProvider, options) =>
        {
            var databaseConnectionString = serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("TaskOpsDatabase");

            if (string.IsNullOrWhiteSpace(databaseConnectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:TaskOpsDatabase must be configured.");
            }

            options.UseNpgsql(databaseConnectionString);
        });
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgresql");

        return services;
    }
}
