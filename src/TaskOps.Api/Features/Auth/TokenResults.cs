namespace TaskOps.Api.Features.Auth;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

public sealed record RefreshTokenResult(string Token, string TokenHash, DateTimeOffset ExpiresAt);
