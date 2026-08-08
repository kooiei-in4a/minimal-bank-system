using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Shared PostgreSQL 18 container owned by the <see cref="PostgreSqlIntegrationFixture"/>.
/// One container instance is started per test collection and disposed after all collection tests complete.
/// </summary>
public sealed class SharedPostgreSqlContainer : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    internal PostgreSqlContainer Container =>
        _container ?? throw new InvalidOperationException("PostgreSQL container has not been started.");

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder(PostgreSqlTestImage.Reference)
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }
}
