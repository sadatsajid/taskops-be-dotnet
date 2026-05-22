using FluentValidation;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Features.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private const int MaxDisplayNameLength = 120;
    private const int MinPasswordLength = 8;

    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .Must(EmailRules.IsValid)
            .WithMessage($"A valid email up to {EmailRules.MaxLength} characters is required.")
            .OverridePropertyName("email");

        RuleFor(request => request.DisplayName)
            .Must(displayName =>
            {
                var trimmed = displayName?.Trim() ?? string.Empty;
                return trimmed.Length is > 0 and <= MaxDisplayNameLength;
            })
            .WithMessage($"Display name must be between 1 and {MaxDisplayNameLength} characters.")
            .OverridePropertyName("displayName");

        RuleFor(request => request.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Password is required.")
            .MinimumLength(MinPasswordLength)
            .WithMessage($"Password must be at least {MinPasswordLength} characters.")
            .OverridePropertyName("password");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .Must(EmailRules.IsValid)
            .WithMessage($"A valid email up to {EmailRules.MaxLength} characters is required.")
            .OverridePropertyName("email");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Password is required.")
            .OverridePropertyName("password");
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required.")
            .OverridePropertyName("refreshToken");
    }
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required.")
            .OverridePropertyName("refreshToken");
    }
}
