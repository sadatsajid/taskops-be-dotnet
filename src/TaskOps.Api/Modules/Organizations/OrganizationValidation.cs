using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Modules.Organizations;

internal static class OrganizationValidation
{
    public const int MaxNameLength = 160;
    public const int MaxSlugLength = 100;
    public static readonly string[] RoleNames = Enum.GetNames<OrganizationRole>();
    public static readonly string ValidRolesMessage = $"Role must be one of {string.Join(", ", RoleNames)}.";

    public static bool IsValidName(string? name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        return trimmedName.Length is > 0 and <= MaxNameLength;
    }

    public static bool IsValidSlug(string? value)
    {
        var slug = NormalizeSlug(value ?? string.Empty);

        if (slug.Length is 0 or > MaxSlugLength)
        {
            return false;
        }

        if (!char.IsLetterOrDigit(slug[0]) || !char.IsLetterOrDigit(slug[^1]))
        {
            return false;
        }

        return slug.All(character =>
            character is '-' ||
            character is >= 'a' and <= 'z' ||
            character is >= '0' and <= '9');
    }

    public static bool TryParseRole(string? value, out OrganizationRole role)
    {
        role = default;
        var trimmed = value?.Trim();

        return !string.IsNullOrWhiteSpace(trimmed) &&
            RoleNames.Any(roleName => string.Equals(roleName, trimmed, StringComparison.OrdinalIgnoreCase)) &&
            Enum.TryParse(trimmed, ignoreCase: true, out role);
    }

    public static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();
}
