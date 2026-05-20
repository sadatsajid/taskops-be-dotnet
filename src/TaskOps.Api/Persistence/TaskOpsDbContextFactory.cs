using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskOps.Api.Persistence;

public sealed class TaskOpsDbContextFactory : IDesignTimeDbContextFactory<TaskOpsDbContext>
{
    public TaskOpsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("src/TaskOps.Api/appsettings.json", optional: true)
            .AddJsonFile("src/TaskOps.Api/appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("TaskOpsDatabase")
            ?? "Host=localhost;Port=5432;Database=taskops;Username=taskops;Password=taskops_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<TaskOpsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new TaskOpsDbContext(optionsBuilder.Options);
    }
}
