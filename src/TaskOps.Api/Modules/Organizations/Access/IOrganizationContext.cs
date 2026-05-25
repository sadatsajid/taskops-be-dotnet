using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Modules.Organizations.Access;

public interface IOrganizationContext
{
    OrganizationMember Membership { get; }
}

public interface IOrganizationContextAccessor
{
    void SetMembership(OrganizationMember membership);
}
