namespace TaskOps.Api.Modules.Projects;

internal static class ProjectValidation
{
    public const int MaxNameLength = 160;
    public const int MaxKeyLength = 20;
    public const int MaxDescriptionLength = 2000;

    public static bool IsValidName(string? name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        return trimmedName.Length is > 0 and <= MaxNameLength;
    }

    public static bool IsValidKey(string? value)
    {
        var key = NormalizeKey(value ?? string.Empty);

        if (key.Length is 0 or > MaxKeyLength)
        {
            return false;
        }

        if (!char.IsLetterOrDigit(key[0]) || !char.IsLetterOrDigit(key[^1]))
        {
            return false;
        }

        return key.All(character =>
            character is '-' ||
            character is >= 'A' and <= 'Z' ||
            character is >= '0' and <= '9');
    }

    public static bool IsValidDescription(string? description) =>
        description is null || description.Trim().Length <= MaxDescriptionLength;

    public static string NormalizeKey(string key) => key.Trim().ToUpperInvariant();

    public static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
