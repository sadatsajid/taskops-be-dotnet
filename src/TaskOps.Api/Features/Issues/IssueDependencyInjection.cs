using FluentValidation;

namespace TaskOps.Api.Features.Issues;

public static class IssueDependencyInjection
{
    public static IServiceCollection AddIssueFeature(this IServiceCollection services)
    {
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IValidator<IssueListQuery>, IssueListQueryValidator>();
        services.AddScoped<IValidator<CreateIssueRequest>, CreateIssueRequestValidator>();
        services.AddScoped<IValidator<UpdateIssueRequest>, UpdateIssueRequestValidator>();
        services.AddScoped<IValidator<ChangeIssueStatusRequest>, ChangeIssueStatusRequestValidator>();
        services.AddScoped<IValidator<ChangeIssuePriorityRequest>, ChangeIssuePriorityRequestValidator>();

        return services;
    }
}
