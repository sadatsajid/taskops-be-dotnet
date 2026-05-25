using TaskOps.Api.Modules.Organizations.Access;
using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Modules.Organizations;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organizations")
            .RequireAuthorization()
            .WithTags("Organizations");

        group.MapGet("", ListOrganizationsAsync)
            .WithName("ListOrganizations");

        group.MapPost("", CreateOrganizationAsync)
            .WithName("CreateOrganization");

        group.MapGet("/{organizationId:guid}", GetOrganizationAsync)
            .WithName("GetOrganization")
            .RequireAuthorization(OrganizationPolicies.Member);

        group.MapPut("/{organizationId:guid}", UpdateOrganizationAsync)
            .WithName("UpdateOrganization")
            .RequireAuthorization(OrganizationPolicies.Owner);

        group.MapGet("/{organizationId:guid}/members", ListMembersAsync)
            .WithName("ListOrganizationMembers")
            .RequireAuthorization(OrganizationPolicies.Member);

        group.MapPost("/{organizationId:guid}/members", AddMemberAsync)
            .WithName("AddOrganizationMember")
            .RequireAuthorization(OrganizationPolicies.Owner);

        group.MapPut("/{organizationId:guid}/members/{memberId:guid}/role", ChangeMemberRoleAsync)
            .WithName("ChangeOrganizationMemberRole")
            .RequireAuthorization(OrganizationPolicies.Owner);

        group.MapDelete("/{organizationId:guid}/members/{memberId:guid}", RemoveMemberAsync)
            .WithName("RemoveOrganizationMember")
            .RequireAuthorization(OrganizationPolicies.Owner);

        return endpoints;
    }

    private static async Task<IResult> ListOrganizationsAsync(
        [AsParameters] PageRequest page,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.ListOrganizationsAsync(page, cancellationToken);
        return EndpointResults.OkOrFailure(result, OrganizationFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.CreateOrganizationAsync(request, cancellationToken);

        return EndpointResults.CreatedOrFailure(
            result,
            OrganizationFailure.None,
            organization => $"/api/organizations/{organization.Id}",
            httpContext,
            ToFailureResult);
    }

    private static async Task<IResult> GetOrganizationAsync(
        Guid organizationId,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.GetOrganizationAsync(organizationId, cancellationToken);
        return EndpointResults.OkOrFailure(result, OrganizationFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.UpdateOrganizationAsync(organizationId, request, cancellationToken);
        return EndpointResults.OkOrFailure(result, OrganizationFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> ListMembersAsync(
        Guid organizationId,
        [AsParameters] PageRequest page,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.ListMembersAsync(organizationId, page, cancellationToken);
        return EndpointResults.OkOrFailure(result, OrganizationFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> AddMemberAsync(
        Guid organizationId,
        AddOrganizationMemberRequest request,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.AddMemberAsync(organizationId, request, cancellationToken);

        return EndpointResults.CreatedOrFailure(
            result,
            OrganizationFailure.None,
            member => $"/api/organizations/{organizationId}/members/{member.Id}",
            httpContext,
            ToFailureResult);
    }

    private static async Task<IResult> ChangeMemberRoleAsync(
        Guid organizationId,
        Guid memberId,
        ChangeOrganizationMemberRoleRequest request,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.ChangeMemberRoleAsync(organizationId, memberId, request, cancellationToken);
        return EndpointResults.OkOrFailure(result, OrganizationFailure.None, httpContext, ToFailureResult);
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid organizationId,
        Guid memberId,
        IOrganizationService organizationService,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.RemoveMemberAsync(organizationId, memberId, cancellationToken);

        return EndpointResults.NoContentOrFailure(result, OrganizationFailure.None, ToFailureResult);
    }

    private static IResult ToFailureResult<T>(ServiceResult<T, OrganizationFailure> result)
    {
        return result.Failure switch
        {
            OrganizationFailure.Validation => EndpointResults.ValidationProblem(result.Errors),
            OrganizationFailure.Unauthorized => EndpointResults.Unauthorized(),
            OrganizationFailure.NotFound => EndpointResults.NotFound(),
            OrganizationFailure.DuplicateSlug => EndpointResults.ConflictProblem(
                "Duplicate organization slug.",
                "An organization with this slug already exists."),
            OrganizationFailure.UserNotFound => EndpointResults.NotFoundProblem(
                "User not found.",
                "No user exists with the supplied email."),
            OrganizationFailure.DuplicateMember => EndpointResults.ConflictProblem(
                "Duplicate organization member.",
                "This user is already a member of the organization."),
            OrganizationFailure.CannotRemoveLastOwner => EndpointResults.ConflictProblem(
                "Organization must have an owner.",
                "The last organization owner cannot be removed or assigned another role."),
            _ => EndpointResults.InternalServerError()
        };
    }
}
