using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskOps.Api.Persistence;
using TaskOps.Api.Tests.Infrastructure;

namespace TaskOps.Api.Tests;

public sealed class PersistenceTests(TaskOpsApiFactory factory) : IntegrationTestBase(factory), IClassFixture<TaskOpsApiFactory>
{
    [Fact]
    public async Task Startup_AppliesMigrationsAndStartsWithEmptyTestDatabase()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();

        var migrations = await dbContext.Database.GetAppliedMigrationsAsync();
        migrations.Should().Contain(migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
        migrations.Should().Contain(migration => migration.EndsWith("_AddRefreshTokens", StringComparison.Ordinal));

        (await dbContext.Users.CountAsync()).Should().Be(0);
        (await dbContext.Organizations.CountAsync()).Should().Be(0);
        (await dbContext.Projects.CountAsync()).Should().Be(0);
        (await dbContext.Issues.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ResetDatabase_RemovesApplicationDataWithoutDroppingMigrations()
    {
        var client = Factory.CreateClient();
        await client.RegisterAsync($"reset-{Guid.NewGuid():N}@example.com", "Reset Test");

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();
            (await dbContext.Users.CountAsync()).Should().Be(1);
        }

        await Factory.ResetDatabaseAsync();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();
            (await dbContext.Users.CountAsync()).Should().Be(0);

            var migrations = await dbContext.Database.GetAppliedMigrationsAsync();
            migrations.Should().Contain(migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
            migrations.Should().Contain(migration => migration.EndsWith("_AddRefreshTokens", StringComparison.Ordinal));
        }
    }
}
