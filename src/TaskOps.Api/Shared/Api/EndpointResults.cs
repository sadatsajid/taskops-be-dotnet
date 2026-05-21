namespace TaskOps.Api.Shared.Api;

public static class EndpointResults
{
    public static IResult Ok<T>(T data, HttpContext httpContext) =>
        Results.Ok(ApiResponse.Success(data, httpContext.TraceIdentifier));

    public static IResult Created<T>(string uri, T data, HttpContext httpContext) =>
        Results.Created(uri, ApiResponse.Success(data, httpContext.TraceIdentifier));

    public static IResult ValidationProblem(IReadOnlyDictionary<string, string[]>? errors) =>
        Results.ValidationProblem(errors?.ToDictionary() ?? []);
}
