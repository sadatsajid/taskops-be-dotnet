using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Features.Issues;

internal static class IssueAccessPolicy
{
    public static bool CanManageIssues(OrganizationRole role) =>
        OrganizationRolePolicies.Allows(OrganizationRolePolicies.ProjectManagement, role);

    public static bool CanAssignedDeveloperChangeStatus(OrganizationMember membership, Guid? assigneeId) =>
        membership.Role == OrganizationRole.Developer && assigneeId == membership.Id;
}
