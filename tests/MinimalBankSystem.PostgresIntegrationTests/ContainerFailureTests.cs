using Npgsql;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// Owns a dedicated, throwaway container instead of joining
/// <see cref="PostgresCollectionDefinition"/> so that stopping the container to prove a
/// lifecycle failure cannot disturb the container shared by the rest of the suite.
/// Because this class is its own default xUnit collection, it runs in parallel with
/// <see cref="PostgresCollectionDefinition"/>.
/// </summary>
public sealed class ContainerFailureTests : IAsyncLifetime
{
    private PostgreSqlContainer? container;

    public async Task InitializeAsync()
    {
        PostgreSqlContainer started = new PostgreSqlBuilder(PostgresImage.Reference).Build();
        await started.StartAsync();
        container = started;
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConnectingAfterTheContainerStopsFailsExplicitlyInsteadOfHangingOrSucceeding()
    {
        string connectionString = container!.GetConnectionString();

        await using (NpgsqlConnection warmup = new(connectionString))
        {
            await warmup.OpenAsync();
        }

        await container.StopAsync();

        await Assert.ThrowsAnyAsync<NpgsqlException>(async () =>
        {
            await using NpgsqlConnection afterStop = new(connectionString);
            await afterStop.OpenAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await using NpgsqlCommand probe = new("SELECT 1", afterStop);
            await probe.ExecuteScalarAsync().WaitAsync(TimeSpan.FromSeconds(10));
        });
    }
}
