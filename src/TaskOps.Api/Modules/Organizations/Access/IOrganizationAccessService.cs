using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Modules.Organizations.Access;

public interface IOrganizationAccessService
{
    Task<OrganizationAccessResult> RequireMembershipAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<OrganizationAccessResult> RequireAnyRoleAsync(
        Guid organizationId,
        IReadOnlyCollection<OrganizationRole> roles,
        CancellationToken cancellationToken);
}
