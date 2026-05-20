namespace TaskOps.Api.Shared.Api;

public sealed record ApiResponse<T>(T Data, string TraceId)
{
    public bool Success => true;
}

public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(T data, string traceId) => new(data, traceId);
}
