using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskOps.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace TaskOps.Api.Tests.Infrastructure;

public sealed class TaskOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("taskops_tests")
        .WithUsername("taskops")
        .WithPassword("taskops_test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:TaskOpsDatabase"] = _postgres.GetConnectionString(),
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Database:SeedDevelopmentData"] = "false",
                ["Jwt:Issuer"] = "TaskOps.Tests",
                ["Jwt:Audience"] = "TaskOps.Api.Tests",
                ["Jwt:SigningKey"] = "integration-test-taskops-signing-key-change-before-production",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "30"
            };

            configurationBuilder.AddInMemoryCollection(overrides);
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE
                "IssueActivities",
                "IssueComments",
                "Issues",
                "Projects",
                "OrganizationMembers",
                "RefreshTokens",
                "Organizations",
                "Users"
            RESTART IDENTITY CASCADE;
            """);
    }
}
