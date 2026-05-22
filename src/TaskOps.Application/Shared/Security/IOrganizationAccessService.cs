using TaskOps.Domain.Entities;

namespace TaskOps.Application.Shared.Security;

public interface IOrganizationAccessService
{
    Task<OrganizationAccessResult> RequireMembershipAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<OrganizationAccessResult> RequireAnyRoleAsync(
        Guid organizationId,
        IReadOnlyCollection<OrganizationRole> roles,
        CancellationToken cancellationToken);
}
