using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskOps.Infrastructure.Persistence;
using TaskOps.Domain.Entities;
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
        migrations.Should().Contain(migration => migration.EndsWith("_AddIssueTenantConstraints", StringComparison.Ordinal));

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
            migrations.Should().Contain(migration => migration.EndsWith("_AddIssueTenantConstraints", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task DatabaseRejectsIssueWhoseProjectBelongsToAnotherOrganization()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();
        var graph = await CreateTenantGraphAsync(dbContext);

        dbContext.Issues.Add(new Issue
        {
            Id = Guid.NewGuid(),
            OrganizationId = graph.FirstOrganizationId,
            ProjectId = graph.SecondProjectId,
            Number = 1,
            Title = "Cross-tenant project",
            Status = IssueStatus.Todo,
            Priority = IssuePriority.Medium
        });

        Func<Task> act = () => dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DatabaseRejectsIssueWhoseAssigneeBelongsToAnotherOrganization()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();
        var graph = await CreateTenantGraphAsync(dbContext);

        dbContext.Issues.Add(new Issue
        {
            Id = Guid.NewGuid(),
            OrganizationId = graph.FirstOrganizationId,
            ProjectId = graph.FirstProjectId,
            Number = 1,
            Title = "Cross-tenant assignee",
            Status = IssueStatus.Todo,
            Priority = IssuePriority.Medium,
            AssigneeId = graph.SecondMemberId
        });

        Func<Task> act = () => dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DeletingAssignedMemberClearsIssueAssignee()
    {
        TenantGraph graph;
        Guid issueId;

        await using (var setupScope = Factory.Services.CreateAsyncScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();
            graph = await CreateTenantGraphAsync(dbContext);

            var issue = new Issue
            {
                Id = Guid.NewGuid(),
                OrganizationId = graph.FirstOrganizationId,
                ProjectId = graph.FirstProjectId,
                Number = 1,
                Title = "Assigned issue",
                Status = IssueStatus.Todo,
                Priority = IssuePriority.Medium,
                AssigneeId = graph.FirstMemberId
            };
            issueId = issue.Id;

            dbContext.Issues.Add(issue);
            await dbContext.SaveChangesAsync();
        }

        await using (var deleteScope = Factory.Services.CreateAsyncScope())
        {
            var dbContext = deleteScope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();
            var member = await dbContext.OrganizationMembers.SingleAsync(member => member.Id == graph.FirstMemberId);

            dbContext.OrganizationMembers.Remove(member);
            await dbContext.SaveChangesAsync();
        }

        await using (var assertScope = Factory.Services.CreateAsyncScope())
        {
            var dbContext = assertScope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();
            var issue = await dbContext.Issues.AsNoTracking().SingleAsync(issue => issue.Id == issueId);

            issue.AssigneeId.Should().BeNull();
            issue.OrganizationId.Should().Be(graph.FirstOrganizationId);
        }
    }

    private static async Task<TenantGraph> CreateTenantGraphAsync(TaskOpsDbContext dbContext)
    {
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        var firstMemberId = Guid.NewGuid();
        var secondMemberId = Guid.NewGuid();
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new User
            {
                Id = firstUserId,
                Email = $"tenant-a-{Guid.NewGuid():N}@example.com",
                NormalizedEmail = $"TENANT-A-{Guid.NewGuid():N}@EXAMPLE.COM",
                DisplayName = "Tenant A User"
            },
            new User
            {
                Id = secondUserId,
                Email = $"tenant-b-{Guid.NewGuid():N}@example.com",
                NormalizedEmail = $"TENANT-B-{Guid.NewGuid():N}@EXAMPLE.COM",
                DisplayName = "Tenant B User"
            });

        dbContext.Organizations.AddRange(
            new Organization
            {
                Id = firstOrganizationId,
                Name = "Tenant A",
                Slug = $"tenant-a-{Guid.NewGuid():N}"[..20]
            },
            new Organization
            {
                Id = secondOrganizationId,
                Name = "Tenant B",
                Slug = $"tenant-b-{Guid.NewGuid():N}"[..20]
            });

        dbContext.OrganizationMembers.AddRange(
            new OrganizationMember
            {
                Id = firstMemberId,
                OrganizationId = firstOrganizationId,
                UserId = firstUserId,
                Role = OrganizationRole.Developer,
                JoinedAt = DateTimeOffset.UtcNow
            },
            new OrganizationMember
            {
                Id = secondMemberId,
                OrganizationId = secondOrganizationId,
                UserId = secondUserId,
                Role = OrganizationRole.Developer,
                JoinedAt = DateTimeOffset.UtcNow
            });

        dbContext.Projects.AddRange(
            new Project
            {
                Id = firstProjectId,
                OrganizationId = firstOrganizationId,
                Name = "Tenant A Project",
                Key = $"A{Guid.NewGuid():N}"[..12]
            },
            new Project
            {
                Id = secondProjectId,
                OrganizationId = secondOrganizationId,
                Name = "Tenant B Project",
                Key = $"B{Guid.NewGuid():N}"[..12]
            });

        await dbContext.SaveChangesAsync();

        return new TenantGraph(
            firstOrganizationId,
            secondOrganizationId,
            firstMemberId,
            secondMemberId,
            firstProjectId,
            secondProjectId);
    }

    private sealed record TenantGraph(
        Guid FirstOrganizationId,
        Guid SecondOrganizationId,
        Guid FirstMemberId,
        Guid SecondMemberId,
        Guid FirstProjectId,
        Guid SecondProjectId);
}
