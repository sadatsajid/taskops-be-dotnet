using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Shared.Security;

public interface IOrganizationAccessService
{
    Task<OrganizationMember?> GetMembershipAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<bool> HasAnyRoleAsync(Guid organizationId, OrganizationRole[] roles, CancellationToken cancellationToken);
}
