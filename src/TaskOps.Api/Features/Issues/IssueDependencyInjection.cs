namespace TaskOps.Api.Features.Issues;

public static class IssueDependencyInjection
{
    public static IServiceCollection AddIssueFeature(this IServiceCollection services)
    {
        services.AddScoped<IIssueService, IssueService>();

        return services;
    }
}
