using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Shared.Security;

public sealed class OrganizationAccessService(
    TaskOpsDbContext dbContext,
    ICurrentUserService currentUser) : IOrganizationAccessService
{
    public async Task<OrganizationMember?> GetMembershipAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return null;
        }

        return await dbContext.OrganizationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                member => member.OrganizationId == organizationId && member.UserId == userId,
                cancellationToken);
    }

    public async Task<bool> HasAnyRoleAsync(Guid organizationId, OrganizationRole[] roles, CancellationToken cancellationToken)
    {
        var membership = await GetMembershipAsync(organizationId, cancellationToken);
        return membership is not null && roles.Contains(membership.Role);
    }
}
