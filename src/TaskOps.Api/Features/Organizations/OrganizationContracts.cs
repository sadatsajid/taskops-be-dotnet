using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Features.Organizations;

public sealed record CreateOrganizationRequest(string Name, string Slug);

public sealed record UpdateOrganizationRequest(string Name, string Slug);

public sealed record AddOrganizationMemberRequest(string Email, string Role);

public sealed record ChangeOrganizationMemberRoleRequest(string Role);

public sealed record PageRequest(int Offset = 0, int Limit = 50)
{
    private const int MaxLimit = 100;

    public int SafeOffset => Math.Max(0, Offset);

    public int SafeLimit => Math.Clamp(Limit, 1, MaxLimit);
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    bool HasMore);

public sealed record OrganizationListItemResponse(
    Guid Id,
    string Name,
    string Slug,
    string Role);

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    OrganizationMemberResponse CurrentMember);

public sealed record OrganizationMemberResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset JoinedAt)
{
    public static string FormatRole(OrganizationRole role) => role.ToString();
}
