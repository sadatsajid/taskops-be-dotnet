using TaskOps.Application.Modules.Organizations.Access;
using TaskOps.Domain.Modules.Organizations;

namespace TaskOps.Api.Modules.Organizations.Access;

internal sealed class OrganizationContext : IOrganizationContext, IOrganizationContextAccessor
{
    private OrganizationMember? _membership;

    public OrganizationMember Membership =>
        _membership ?? throw new InvalidOperationException(
            "Organization membership has not been resolved for this request. " +
            "Ensure the endpoint requires an organization authorization policy.");

    public void SetMembership(OrganizationMember membership) => _membership = membership;
}
