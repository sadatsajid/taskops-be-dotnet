using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Shared.Security;

public interface IOrganizationAccessService
{
    Task<OrganizationAccessResult> RequireMembershipAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<OrganizationAccessResult> RequireAnyRoleAsync(
        Guid organizationId,
        OrganizationRole[] roles,
        CancellationToken cancellationToken);
}
