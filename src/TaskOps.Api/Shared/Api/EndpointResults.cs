namespace TaskOps.Api.Shared.Api;

public static class EndpointResults
{
    public static IResult Ok<T>(T data, HttpContext httpContext) =>
        Results.Ok(ApiResponse.Success(data, httpContext.TraceIdentifier));

    public static IResult Created<T>(string uri, T data, HttpContext httpContext) =>
        Results.Created(uri, ApiResponse.Success(data, httpContext.TraceIdentifier));

    public static IResult OkOrFailure<T, TFailure>(
        ServiceResult<T, TFailure> result,
        TFailure noneFailure,
        HttpContext httpContext,
        Func<ServiceResult<T, TFailure>, IResult> failureResult) =>
        result.IsSuccess(noneFailure)
            ? Ok(result.Value!, httpContext)
            : failureResult(result);

    public static IResult CreatedOrFailure<T, TFailure>(
        ServiceResult<T, TFailure> result,
        TFailure noneFailure,
        Func<T, string> uriFactory,
        HttpContext httpContext,
        Func<ServiceResult<T, TFailure>, IResult> failureResult) =>
        result.IsSuccess(noneFailure)
            ? Created(uriFactory(result.Value!), result.Value!, httpContext)
            : failureResult(result);

    public static IResult NoContentOrFailure<T, TFailure>(
        ServiceResult<T, TFailure> result,
        TFailure noneFailure,
        Func<ServiceResult<T, TFailure>, IResult> failureResult) =>
        result.IsSuccess(noneFailure)
            ? Results.NoContent()
            : failureResult(result);

    public static IResult ValidationProblem(IReadOnlyDictionary<string, string[]>? errors) =>
        Results.ValidationProblem(errors?.ToDictionary() ?? []);

    public static IResult Unauthorized() => Results.Unauthorized();

    public static IResult UnauthorizedProblem(string title, string? detail = null) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized);

    public static IResult NotFound() => Results.NotFound();

    public static IResult NotFoundProblem(string title, string detail) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status404NotFound);

    public static IResult ForbiddenProblem(string detail) =>
        Results.Problem(
            title: "Forbidden.",
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden);

    public static IResult BadRequestProblem(string title, string detail) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);

    public static IResult ConflictProblem(string title, string detail) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);

    public static IResult InternalServerError() =>
        Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
}
