using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.Fixtures;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private const string ImageReference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    private PostgreSqlContainer? _container;

    public PostgreSqlContainer Container =>
        _container ?? throw new InvalidOperationException("Container not initialized.");

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder(ImageReference)
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public string GetConnectionString() =>
        _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not started.");
}
