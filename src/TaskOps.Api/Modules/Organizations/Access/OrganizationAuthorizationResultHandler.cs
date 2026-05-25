using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace TaskOps.Api.Modules.Organizations.Access;

public sealed class OrganizationAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler Default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && authorizeResult.AuthorizationFailure is { } failure)
        {
            foreach (var reason in failure.FailureReasons)
            {
                switch (reason.Message)
                {
                    case OrganizationAccessFailureReasons.NotMember:
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    case OrganizationAccessFailureReasons.Unauthenticated:
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    case OrganizationAccessFailureReasons.RoleNotAllowed:
                        await WriteForbiddenProblemAsync(context);
                        return;
                }
            }
        }

        await Default.HandleAsync(next, context, policy, authorizeResult);
    }

    private static async Task WriteForbiddenProblemAsync(HttpContext context)
    {
        var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Title = "Forbidden.",
                Detail = "The current user does not have the required organization role.",
                Status = StatusCodes.Status403Forbidden,
                Instance = context.Request.Path
            }
        });
    }
}
