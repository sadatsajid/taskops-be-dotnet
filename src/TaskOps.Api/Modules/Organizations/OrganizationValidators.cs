using FluentValidation;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Modules.Organizations;

public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(OrganizationValidation.IsValidName)
            .WithMessage($"Name must be between 1 and {OrganizationValidation.MaxNameLength} characters.")
            .OverridePropertyName("name");

        RuleFor(request => request.Slug)
            .Must(OrganizationValidation.IsValidSlug)
            .WithMessage($"Slug must be between 1 and {OrganizationValidation.MaxSlugLength} characters and contain only lowercase letters, numbers, and hyphens.")
            .OverridePropertyName("slug");
    }
}

public sealed class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(OrganizationValidation.IsValidName)
            .WithMessage($"Name must be between 1 and {OrganizationValidation.MaxNameLength} characters.")
            .OverridePropertyName("name");

        RuleFor(request => request.Slug)
            .Must(OrganizationValidation.IsValidSlug)
            .WithMessage($"Slug must be between 1 and {OrganizationValidation.MaxSlugLength} characters and contain only lowercase letters, numbers, and hyphens.")
            .OverridePropertyName("slug");
    }
}

public sealed class AddOrganizationMemberRequestValidator : AbstractValidator<AddOrganizationMemberRequest>
{
    public AddOrganizationMemberRequestValidator()
    {
        RuleFor(request => request.Email)
            .Must(EmailRules.IsValid)
            .WithMessage($"A valid email up to {EmailRules.MaxLength} characters is required.")
            .OverridePropertyName("email");

        RuleFor(request => request.Role)
            .Must(role => OrganizationValidation.TryParseRole(role, out _))
            .WithMessage(OrganizationValidation.ValidRolesMessage)
            .OverridePropertyName("role");
    }
}

public sealed class ChangeOrganizationMemberRoleRequestValidator : AbstractValidator<ChangeOrganizationMemberRoleRequest>
{
    public ChangeOrganizationMemberRoleRequestValidator()
    {
        RuleFor(request => request.Role)
            .Must(role => OrganizationValidation.TryParseRole(role, out _))
            .WithMessage(OrganizationValidation.ValidRolesMessage)
            .OverridePropertyName("role");
    }
}
