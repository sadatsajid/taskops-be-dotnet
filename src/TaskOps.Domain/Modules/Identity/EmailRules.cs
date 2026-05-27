namespace TaskOps.Domain.Modules.Identity;

public static class EmailRules
{
    public const int MaxLength = 320;

    public static Dictionary<string, string[]> Validate(string? email, string fieldName = "email")
    {
        var errors = new Dictionary<string, string[]>();

        if (!IsValid(email))
        {
            errors[fieldName] = [$"A valid email up to {MaxLength} characters is required."];
        }

        return errors;
    }

    public static bool IsValid(string? email)
    {
        var trimmedEmail = email?.Trim() ?? string.Empty;

        return trimmedEmail.Length > 0 &&
            trimmedEmail.Length <= MaxLength &&
            trimmedEmail.Contains('@', StringComparison.Ordinal);
    }

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
