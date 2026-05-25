using TaskOps.Api.Modules.Organizations.Access;
using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Modules.Issues;

internal static class IssueAccessPolicy
{
    public static bool CanManageIssues(OrganizationRole role) =>
        OrganizationRolePolicies.Allows(OrganizationRolePolicies.ProjectManagement, role);

    public static bool CanAssignedDeveloperChangeStatus(OrganizationMember membership, Guid? assigneeId) =>
        membership.Role == OrganizationRole.Developer && assigneeId == membership.Id;
}
