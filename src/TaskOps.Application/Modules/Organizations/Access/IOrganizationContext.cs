using TaskOps.Domain.Modules.Organizations;

namespace TaskOps.Application.Modules.Organizations.Access;

public interface IOrganizationContext
{
    OrganizationMember Membership { get; }
}

public interface IOrganizationContextAccessor
{
    void SetMembership(OrganizationMember membership);
}
