using FluentValidation;
using TaskOps.Domain.Entities;

namespace TaskOps.Application.Features.Issues;

public sealed class IssueListQueryValidator : AbstractValidator<IssueListQuery>
{
    public IssueListQueryValidator()
    {
        RuleFor(query => query.Status)
            .Must(IssueValidation.IsValidOptionalNamedEnum<IssueStatus>)
            .WithMessage(IssueValidation.StatusMessage)
            .OverridePropertyName("status");

        RuleFor(query => query.Priority)
            .Must(IssueValidation.IsValidOptionalNamedEnum<IssuePriority>)
            .WithMessage(IssueValidation.PriorityMessage)
            .OverridePropertyName("priority");

        RuleFor(query => query.CreatedFrom)
            .Must((query, createdFrom) => createdFrom is null || query.CreatedTo is null || createdFrom <= query.CreatedTo)
            .WithMessage("CreatedFrom must be on or before CreatedTo.")
            .OverridePropertyName("createdFrom");

        RuleFor(query => query.DueFrom)
            .Must((query, dueFrom) => dueFrom is null || query.DueTo is null || dueFrom <= query.DueTo)
            .WithMessage("DueFrom must be on or before DueTo.")
            .OverridePropertyName("dueFrom");

        RuleFor(query => query.Search)
            .Must(IssueValidation.IsValidSearch)
            .WithMessage($"Search must be {IssueValidation.MaxSearchLength} characters or fewer.")
            .OverridePropertyName("search");

        RuleFor(query => query.Sort)
            .Must(IssueValidation.IsValidSort)
            .WithMessage(IssueValidation.SortMessage)
            .OverridePropertyName("sort");
    }
}

public sealed class CreateIssueRequestValidator : AbstractValidator<CreateIssueRequest>
{
    public CreateIssueRequestValidator()
    {
        RuleFor(request => request.Title)
            .Must(IssueValidation.IsValidTitle)
            .WithMessage($"Title must be between 1 and {IssueValidation.MaxTitleLength} characters.")
            .OverridePropertyName("title");

        RuleFor(request => request.Description)
            .Must(IssueValidation.IsValidDescription)
            .WithMessage($"Description must be {IssueValidation.MaxDescriptionLength} characters or fewer.")
            .OverridePropertyName("description");

        RuleFor(request => request.Priority)
            .Must(IssueValidation.IsValidRequiredNamedEnum<IssuePriority>)
            .WithMessage(IssueValidation.PriorityMessage)
            .OverridePropertyName("priority");
    }
}

public sealed class UpdateIssueRequestValidator : AbstractValidator<UpdateIssueRequest>
{
    public UpdateIssueRequestValidator()
    {
        RuleFor(request => request.Title)
            .Must(IssueValidation.IsValidTitle)
            .WithMessage($"Title must be between 1 and {IssueValidation.MaxTitleLength} characters.")
            .OverridePropertyName("title");

        RuleFor(request => request.Description)
            .Must(IssueValidation.IsValidDescription)
            .WithMessage($"Description must be {IssueValidation.MaxDescriptionLength} characters or fewer.")
            .OverridePropertyName("description");
    }
}

public sealed class ChangeIssueStatusRequestValidator : AbstractValidator<ChangeIssueStatusRequest>
{
    public ChangeIssueStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(IssueValidation.IsValidRequiredNamedEnum<IssueStatus>)
            .WithMessage(IssueValidation.StatusMessage)
            .OverridePropertyName("status");
    }
}

public sealed class ChangeIssuePriorityRequestValidator : AbstractValidator<ChangeIssuePriorityRequest>
{
    public ChangeIssuePriorityRequestValidator()
    {
        RuleFor(request => request.Priority)
            .Must(IssueValidation.IsValidRequiredNamedEnum<IssuePriority>)
            .WithMessage(IssueValidation.PriorityMessage)
            .OverridePropertyName("priority");
    }
}
