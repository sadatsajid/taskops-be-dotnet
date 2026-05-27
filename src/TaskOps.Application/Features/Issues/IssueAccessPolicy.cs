using TaskOps.Domain.Entities;
using TaskOps.Domain.Security;

namespace TaskOps.Application.Features.Issues;

public static class IssueAccessPolicy
{
    public static bool CanManageIssues(OrganizationRole role) =>
        OrganizationRolePolicies.Allows(OrganizationRolePolicies.ProjectManagement, role);

    public static bool CanAssignedDeveloperChangeStatus(OrganizationMember membership, Guid? assigneeId) =>
        membership.Role == OrganizationRole.Developer && assigneeId == membership.Id;
}
