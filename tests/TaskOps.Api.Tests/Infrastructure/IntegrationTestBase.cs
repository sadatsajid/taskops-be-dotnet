namespace TaskOps.Api.Tests.Infrastructure;

public abstract class IntegrationTestBase(TaskOpsApiFactory factory) : IAsyncLifetime
{
    protected TaskOpsApiFactory Factory { get; } = factory;

    public virtual Task InitializeAsync()
    {
        return Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
