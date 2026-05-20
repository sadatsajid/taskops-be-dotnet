namespace TaskOps.Api.Tests;

public sealed record ApiResponseEnvelope<T>(T Data, string TraceId, bool Success);
