using Microsoft.AspNetCore.Authorization;
using TaskOps.Application.Modules.Organizations.Access;

namespace TaskOps.Api.Modules.Organizations.Access;

public sealed class OrganizationMembershipHandler(
    IOrganizationAccessService accessService,
    IOrganizationContextAccessor contextAccessor)
    : AuthorizationHandler<OrganizationMembershipRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrganizationMembershipRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            context.Fail(new AuthorizationFailureReason(this, OrganizationAccessFailureReasons.MissingOrganizationRoute));
            return;
        }

        if (!httpContext.Request.RouteValues.TryGetValue("organizationId", out var routeValue) ||
            !Guid.TryParse(routeValue?.ToString(), out var organizationId))
        {
            context.Fail(new AuthorizationFailureReason(this, OrganizationAccessFailureReasons.MissingOrganizationRoute));
            return;
        }

        var cancellationToken = httpContext.RequestAborted;
        var access = requirement.AllowedRoles is { Count: > 0 } roles
            ? await accessService.RequireAnyRoleAsync(organizationId, roles, cancellationToken)
            : await accessService.RequireMembershipAsync(organizationId, cancellationToken);

        switch (access.Status)
        {
            case OrganizationAccessStatus.Allowed:
                contextAccessor.SetMembership(access.Membership!);
                context.Succeed(requirement);
                return;
            case OrganizationAccessStatus.Unauthorized:
                context.Fail(new AuthorizationFailureReason(this, OrganizationAccessFailureReasons.Unauthenticated));
                return;
            case OrganizationAccessStatus.NotFound:
                context.Fail(new AuthorizationFailureReason(this, OrganizationAccessFailureReasons.NotMember));
                return;
            case OrganizationAccessStatus.Forbidden:
                contextAccessor.SetMembership(access.Membership!);
                context.Fail(new AuthorizationFailureReason(this, OrganizationAccessFailureReasons.RoleNotAllowed));
                return;
        }
    }
}
