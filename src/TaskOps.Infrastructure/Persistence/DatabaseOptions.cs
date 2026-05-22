namespace TaskOps.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public bool ApplyMigrationsOnStartup { get; set; }

    public bool SeedDevelopmentData { get; set; }
}
