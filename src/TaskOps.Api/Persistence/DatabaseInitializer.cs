using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        var databaseOptions = configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();

        if (!databaseOptions.ApplyMigrationsOnStartup && !databaseOptions.SeedDevelopmentData)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();

        if (databaseOptions.ApplyMigrationsOnStartup)
        {
            await dbContext.Database.MigrateAsync();
        }

        if ((environment.IsDevelopment() || environment.IsEnvironment("Testing")) && databaseOptions.SeedDevelopmentData)
        {
            await SeedDevelopmentDataAsync(dbContext);
        }
    }

    private static async Task SeedDevelopmentDataAsync(TaskOpsDbContext dbContext)
    {
        if (await dbContext.Users.AnyAsync())
        {
            return;
        }

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var organizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var memberId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var projectId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var user = new User
        {
            Id = userId,
            Email = "owner@taskops.local",
            NormalizedEmail = "OWNER@TASKOPS.LOCAL",
            DisplayName = "TaskOps Owner",
            PasswordHash = null
        };

        var organization = new Organization
        {
            Id = organizationId,
            Name = "TaskOps Demo",
            Slug = "taskops-demo"
        };

        var member = new OrganizationMember
        {
            Id = memberId,
            UserId = userId,
            OrganizationId = organizationId,
            Role = OrganizationRole.Owner,
            JoinedAt = DateTimeOffset.UtcNow
        };

        var project = new Project
        {
            Id = projectId,
            OrganizationId = organizationId,
            Name = "TaskOps API",
            Key = "API",
            Description = "The first backend project for learning pragmatic .NET API development."
        };

        var issue = new Issue
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            OrganizationId = organizationId,
            ProjectId = projectId,
            Number = 1,
            Title = "Finish Phase 2 database foundation",
            Description = "Docker PostgreSQL, EF Core DbContext, initial entities, migration, and development seed data.",
            Status = IssueStatus.Todo,
            Priority = IssuePriority.High,
            AssigneeId = memberId
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMembers.Add(member);
        dbContext.Projects.Add(project);
        dbContext.Issues.Add(issue);

        await dbContext.SaveChangesAsync();
    }
}
