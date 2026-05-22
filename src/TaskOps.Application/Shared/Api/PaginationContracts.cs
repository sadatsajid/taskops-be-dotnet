namespace TaskOps.Application.Shared.Api;

public sealed record PageRequest(int Offset = 0, int Limit = 50)
{
    private const int MaxLimit = 100;

    public int SafeOffset => Math.Max(0, Offset);

    public int SafeLimit => Math.Clamp(Limit, 1, MaxLimit);
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    bool HasMore);
