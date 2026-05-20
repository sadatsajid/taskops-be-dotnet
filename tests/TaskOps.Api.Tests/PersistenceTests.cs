using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskOps.Api.Persistence;
using TaskOps.Api.Tests.Infrastructure;

namespace TaskOps.Api.Tests;

public sealed class PersistenceTests(TaskOpsApiFactory factory) : IClassFixture<TaskOpsApiFactory>
{
    [Fact]
    public async Task Startup_AppliesMigrationsAndSeedsDevelopmentData()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();

        var migrations = await dbContext.Database.GetAppliedMigrationsAsync();
        migrations.Should().Contain(migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
        migrations.Should().Contain(migration => migration.EndsWith("_AddRefreshTokens", StringComparison.Ordinal));

        (await dbContext.Users.CountAsync()).Should().Be(1);
        (await dbContext.Organizations.CountAsync()).Should().Be(1);
        (await dbContext.Projects.CountAsync()).Should().Be(1);
        (await dbContext.Issues.CountAsync()).Should().Be(1);
    }
}
