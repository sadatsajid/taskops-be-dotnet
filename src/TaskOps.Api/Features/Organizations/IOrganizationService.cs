namespace TaskOps.Api.Features.Organizations;

public interface IOrganizationService
{
    Task<OrganizationServiceResult<IReadOnlyList<OrganizationListItemResponse>>> ListOrganizationsAsync(
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

    Task<OrganizationServiceResult<IReadOnlyList<OrganizationMemberResponse>>> ListMembersAsync(
        Guid organizationId,
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
