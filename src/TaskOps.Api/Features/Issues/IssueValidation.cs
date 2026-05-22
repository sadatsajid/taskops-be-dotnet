namespace TaskOps.Api.Features.Issues;

internal static class IssueValidation
{
    public const int MaxTitleLength = 240;
    public const int MaxDescriptionLength = 8000;
    public const int MaxSearchLength = 120;
    public const string StatusMessage = "Status must be one of: Todo, InProgress, InReview, Done.";
    public const string PriorityMessage = "Priority must be one of: Low, Medium, High, Critical.";
    public const string SortMessage = "Sort must be one of: createdAt, dueDate, priority, status, title, number, or the same value prefixed with '-'.";

    public static bool IsValidTitle(string? title)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        return trimmedTitle.Length is > 0 and <= MaxTitleLength;
    }

    public static bool IsValidDescription(string? description) =>
        description is null || description.Trim().Length <= MaxDescriptionLength;

    public static bool IsValidSearch(string? search) =>
        NormalizeOptional(search) is not { } normalizedSearch || normalizedSearch.Length <= MaxSearchLength;

    public static bool IsValidOptionalNamedEnum<TEnum>(string? value)
        where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) || TryParseNamedEnum<TEnum>(value, out _);

    public static bool IsValidRequiredNamedEnum<TEnum>(string? value)
        where TEnum : struct, Enum =>
        TryParseNamedEnum<TEnum>(value, out _);

    public static bool TryParseNamedEnum<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.All(char.IsDigit))
        {
            return false;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out result) && Enum.IsDefined(result);
    }

    public static bool IsValidSort(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return true;
        }

        return normalized is
            "createdAt" or "created" or
            "-createdAt" or "-created" or
            "dueDate" or "due" or
            "-dueDate" or "-due" or
            "priority" or "-priority" or
            "status" or "-status" or
            "title" or "-title" or
            "number" or "-number";
    }

    public static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
