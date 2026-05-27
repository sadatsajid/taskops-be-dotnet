using Microsoft.EntityFrameworkCore;
using TaskOps.Application.Modules.Organizations.Access;
using TaskOps.Application.SharedKernel.Security;
using TaskOps.Domain.Modules.Organizations;
using TaskOps.Infrastructure.Persistence;

namespace TaskOps.Infrastructure.Modules.Organizations.Access;

public sealed class OrganizationAccessService(
    TaskOpsDbContext dbContext,
    ICurrentUserService currentUser) : IOrganizationAccessService
{
    public async Task<OrganizationAccessResult> RequireMembershipAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return OrganizationAccessResult.Unauthorized();
        }

        var membership = await dbContext.OrganizationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                member => member.OrganizationId == organizationId && member.UserId == userId,
                cancellationToken);

        return membership is null
            ? OrganizationAccessResult.NotFound()
            : OrganizationAccessResult.Allowed(membership);
    }

    public async Task<OrganizationAccessResult> RequireAnyRoleAsync(
        Guid organizationId,
        IReadOnlyCollection<OrganizationRole> roles,
        CancellationToken cancellationToken)
    {
        var access = await RequireMembershipAsync(organizationId, cancellationToken);
        if (!access.IsAllowed)
        {
            return access;
        }

        return OrganizationRolePolicies.Allows(roles, access.Membership!.Role)
            ? access
            : OrganizationAccessResult.Forbidden(access.Membership);
    }
}
