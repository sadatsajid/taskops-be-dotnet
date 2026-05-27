using TaskOps.Domain.Modules.Organizations;

namespace TaskOps.Application.Modules.Organizations.Access;

public enum OrganizationAccessStatus
{
    Allowed,
    Unauthorized,
    NotFound,
    Forbidden
}

public sealed record OrganizationAccessResult(
    OrganizationAccessStatus Status,
    OrganizationMember? Membership = null)
{
    public bool IsAllowed => Status == OrganizationAccessStatus.Allowed && Membership is not null;

    public static OrganizationAccessResult Allowed(OrganizationMember membership) =>
        new(OrganizationAccessStatus.Allowed, membership);

    public static OrganizationAccessResult Unauthorized() => new(OrganizationAccessStatus.Unauthorized);

    public static OrganizationAccessResult NotFound() => new(OrganizationAccessStatus.NotFound);

    public static OrganizationAccessResult Forbidden(OrganizationMember membership) =>
        new(OrganizationAccessStatus.Forbidden, membership);
}
