using Microsoft.EntityFrameworkCore;
using TaskOps.Infrastructure.Persistence;

namespace TaskOps.Infrastructure.Features.Issues;

internal static class IssueNumbering
{
    public static async Task<Guid?> TryLockActiveProjectAsync(
        TaskOpsDbContext dbContext,
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Projects
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Projects"
                WHERE "OrganizationId" = {organizationId}
                  AND "Id" = {projectId}
                  AND NOT "IsArchived"
                FOR UPDATE
                """)
            .Select(project => (Guid?)project.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<int> GetNextNumberAsync(
        TaskOpsDbContext dbContext,
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var currentMax = await dbContext.Issues
            .Where(issue => issue.OrganizationId == organizationId && issue.ProjectId == projectId)
            .MaxAsync(issue => (int?)issue.Number, cancellationToken);

        return (currentMax ?? 0) + 1;
    }
}
