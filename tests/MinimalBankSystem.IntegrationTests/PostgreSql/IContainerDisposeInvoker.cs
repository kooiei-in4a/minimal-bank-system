using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

internal interface IContainerDisposeInvoker
{
    ValueTask InvokeAsync(PostgreSqlContainer container, CancellationToken cancellationToken = default);
}

internal sealed class TestcontainersContainerDisposeInvoker : IContainerDisposeInvoker
{
    public ValueTask InvokeAsync(PostgreSqlContainer container, CancellationToken cancellationToken = default) =>
        container.DisposeAsync();
}
