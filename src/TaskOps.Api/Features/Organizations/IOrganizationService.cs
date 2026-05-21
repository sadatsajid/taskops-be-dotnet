namespace TaskOps.Api.Features.Organizations;

public interface IOrganizationService
{
    Task<OrganizationServiceResult<PagedResponse<OrganizationListItemResponse>>> ListOrganizationsAsync(
        PageRequest page,
        CancellationToken cancellationToken);

    Task<OrganizationServiceResult<OrganizationResponse>> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken);

    Task<OrganizationServiceResult<OrganizationResponse>> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<OrganizationServiceResult<OrganizationResponse>> UpdateOrganizationAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken);

    Task<OrganizationServiceResult<PagedResponse<OrganizationMemberResponse>>> ListMembersAsync(
        Guid organizationId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<OrganizationServiceResult<OrganizationMemberResponse>> AddMemberAsync(
        Guid organizationId,
        AddOrganizationMemberRequest request,
        CancellationToken cancellationToken);

    Task<OrganizationServiceResult<OrganizationMemberResponse>> ChangeMemberRoleAsync(
        Guid organizationId,
        Guid memberId,
        ChangeOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken);

    Task<OrganizationServiceResult<object>> RemoveMemberAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken);
}
