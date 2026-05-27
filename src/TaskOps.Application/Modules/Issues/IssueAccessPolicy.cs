using TaskOps.Domain.Modules.Organizations;

namespace TaskOps.Application.Modules.Issues;

public static class IssueAccessPolicy
{
    public static bool CanManageIssues(OrganizationRole role) =>
        OrganizationRolePolicies.Allows(OrganizationRolePolicies.ProjectManagement, role);

    public static bool CanAssignedDeveloperChangeStatus(OrganizationMember membership, Guid? assigneeId) =>
        membership.Role == OrganizationRole.Developer && assigneeId == membership.Id;
}
