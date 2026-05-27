using Microsoft.AspNetCore.Authorization;
using TaskOps.Domain.Modules.Identity;
using TaskOps.Domain.Modules.Issues;
using TaskOps.Domain.Modules.Organizations;
using TaskOps.Domain.Modules.Projects;
using TaskOps.Domain.SharedKernel;

namespace TaskOps.Api.Modules.Organizations.Access;

public static class OrganizationPolicies
{
    public const string Member = "Organization.Member";
    public const string Owner = "Organization.Owner";
    public const string ProjectManagement = "Organization.ProjectManagement";
}

public sealed class OrganizationMembershipRequirement(IReadOnlyCollection<OrganizationRole>? allowedRoles = null)
    : IAuthorizationRequirement
{
    public IReadOnlyCollection<OrganizationRole>? AllowedRoles { get; } = allowedRoles;
}

public static class OrganizationAccessFailureReasons
{
    public const string MissingOrganizationRoute = "organization.route_missing";
    public const string Unauthenticated = "organization.unauthenticated";
    public const string NotMember = "organization.not_member";
    public const string RoleNotAllowed = "organization.role_not_allowed";
}
