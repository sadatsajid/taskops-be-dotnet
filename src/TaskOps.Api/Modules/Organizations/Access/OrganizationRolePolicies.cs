using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Modules.Organizations.Access;

public static class OrganizationRolePolicies
{
    private static readonly OrganizationRole[] OwnerRoleSet = [OrganizationRole.Owner];

    private static readonly OrganizationRole[] ProjectManagementRoleSet =
    [
        OrganizationRole.Owner,
        OrganizationRole.Admin,
        OrganizationRole.ProjectManager
    ];

    public static IReadOnlyCollection<OrganizationRole> OwnerOnly => OwnerRoleSet;

    public static IReadOnlyCollection<OrganizationRole> ProjectManagement => ProjectManagementRoleSet;

    public static bool Allows(IReadOnlyCollection<OrganizationRole> roles, OrganizationRole role) =>
        roles.Contains(role);
}
