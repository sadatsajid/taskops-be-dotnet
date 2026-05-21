using TaskOps.Api.Shared.Api;

namespace TaskOps.Api.Features.Organizations;

public interface IOrganizationService
{
    Task<ServiceResult<PagedResponse<OrganizationListItemResponse>, OrganizationFailure>> ListOrganizationsAsync(
        PageRequest page,
        CancellationToken cancellationToken);

    Task<ServiceResult<OrganizationResponse, OrganizationFailure>> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<OrganizationResponse, OrganizationFailure>> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<ServiceResult<OrganizationResponse, OrganizationFailure>> UpdateOrganizationAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<PagedResponse<OrganizationMemberResponse>, OrganizationFailure>> ListMembersAsync(
        Guid organizationId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<ServiceResult<OrganizationMemberResponse, OrganizationFailure>> AddMemberAsync(
        Guid organizationId,
        AddOrganizationMemberRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<OrganizationMemberResponse, OrganizationFailure>> ChangeMemberRoleAsync(
        Guid organizationId,
        Guid memberId,
        ChangeOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<object, OrganizationFailure>> RemoveMemberAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken);
}
