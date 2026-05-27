using FluentValidation;

namespace TaskOps.Application.Features.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(ProjectValidation.IsValidName)
            .WithMessage($"Name must be between 1 and {ProjectValidation.MaxNameLength} characters.")
            .OverridePropertyName("name");

        RuleFor(request => request.Key)
            .Must(ProjectValidation.IsValidKey)
            .WithMessage($"Key must be between 1 and {ProjectValidation.MaxKeyLength} characters and contain only uppercase letters, numbers, and hyphens.")
            .OverridePropertyName("key");

        RuleFor(request => request.Description)
            .Must(ProjectValidation.IsValidDescription)
            .WithMessage($"Description must be {ProjectValidation.MaxDescriptionLength} characters or fewer.")
            .OverridePropertyName("description");
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(ProjectValidation.IsValidName)
            .WithMessage($"Name must be between 1 and {ProjectValidation.MaxNameLength} characters.")
            .OverridePropertyName("name");

        RuleFor(request => request.Key)
            .Must(ProjectValidation.IsValidKey)
            .WithMessage($"Key must be between 1 and {ProjectValidation.MaxKeyLength} characters and contain only uppercase letters, numbers, and hyphens.")
            .OverridePropertyName("key");

        RuleFor(request => request.Description)
            .Must(ProjectValidation.IsValidDescription)
            .WithMessage($"Description must be {ProjectValidation.MaxDescriptionLength} characters or fewer.")
            .OverridePropertyName("description");
    }
}
