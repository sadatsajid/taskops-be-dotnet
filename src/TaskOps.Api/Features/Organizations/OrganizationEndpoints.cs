using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Features.Organizations;

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
            .WithName("GetOrganization");

        group.MapPut("/{organizationId:guid}", UpdateOrganizationAsync)
            .WithName("UpdateOrganization");

        group.MapGet("/{organizationId:guid}/members", ListMembersAsync)
            .WithName("ListOrganizationMembers");

        group.MapPost("/{organizationId:guid}/members", AddMemberAsync)
            .WithName("AddOrganizationMember");

        group.MapPut("/{organizationId:guid}/members/{memberId:guid}/role", ChangeMemberRoleAsync)
            .WithName("ChangeOrganizationMemberRole");

        group.MapDelete("/{organizationId:guid}/members/{memberId:guid}", RemoveMemberAsync)
            .WithName("RemoveOrganizationMember");

        return endpoints;
    }

    private static async Task<IResult> ListOrganizationsAsync(
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.ListOrganizationsAsync(cancellationToken);
        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.CreateOrganizationAsync(request, cancellationToken);

        return result.Failure == OrganizationFailure.None && result.Value is not null
            ? Results.Created($"/api/organizations/{result.Value.Id}", ApiResponse.Success(result.Value, httpContext.TraceIdentifier))
            : ToFailureResult(result);
    }

    private static async Task<IResult> GetOrganizationAsync(
        Guid organizationId,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.GetOrganizationAsync(organizationId, cancellationToken);
        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.UpdateOrganizationAsync(organizationId, request, cancellationToken);
        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> ListMembersAsync(
        Guid organizationId,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.ListMembersAsync(organizationId, cancellationToken);
        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> AddMemberAsync(
        Guid organizationId,
        AddOrganizationMemberRequest request,
        IOrganizationService organizationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.AddMemberAsync(organizationId, request, cancellationToken);

        return result.Failure == OrganizationFailure.None && result.Value is not null
            ? Results.Created($"/api/organizations/{organizationId}/members/{result.Value.Id}", ApiResponse.Success(result.Value, httpContext.TraceIdentifier))
            : ToFailureResult(result);
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
        return ToOkResult(result, httpContext);
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid organizationId,
        Guid memberId,
        IOrganizationService organizationService,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.RemoveMemberAsync(organizationId, memberId, cancellationToken);

        return result.Failure == OrganizationFailure.None
            ? Results.NoContent()
            : ToFailureResult(result);
    }

    private static IResult ToOkResult<T>(OrganizationServiceResult<T> result, HttpContext httpContext)
    {
        return result.Failure == OrganizationFailure.None && result.Value is not null
            ? Results.Ok(ApiResponse.Success(result.Value, httpContext.TraceIdentifier))
            : ToFailureResult(result);
    }

    private static IResult ToFailureResult<T>(OrganizationServiceResult<T> result)
    {
        return result.Failure switch
        {
            OrganizationFailure.Validation => Results.ValidationProblem(result.Errors?.ToDictionary() ?? []),
            OrganizationFailure.Unauthorized => Results.Unauthorized(),
            OrganizationFailure.Forbidden => Results.Problem(
                title: "Forbidden.",
                detail: "The current user does not have the required organization role.",
                statusCode: StatusCodes.Status403Forbidden),
            OrganizationFailure.NotFound => Results.NotFound(),
            OrganizationFailure.DuplicateSlug => Results.Problem(
                title: "Duplicate organization slug.",
                detail: "An organization with this slug already exists.",
                statusCode: StatusCodes.Status409Conflict),
            OrganizationFailure.UserNotFound => Results.Problem(
                title: "User not found.",
                detail: "No user exists with the supplied email.",
                statusCode: StatusCodes.Status404NotFound),
            OrganizationFailure.DuplicateMember => Results.Problem(
                title: "Duplicate organization member.",
                detail: "This user is already a member of the organization.",
                statusCode: StatusCodes.Status409Conflict),
            OrganizationFailure.CannotRemoveLastOwner => Results.Problem(
                title: "Organization must have an owner.",
                detail: "The last organization owner cannot be removed or assigned another role.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
