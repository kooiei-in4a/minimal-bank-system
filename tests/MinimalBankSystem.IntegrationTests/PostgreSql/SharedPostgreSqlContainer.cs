using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Process-wide shared PostgreSQL 18 Testcontainers instance.
/// Ownership: one container per test host process. Startup is serialized.
/// Database ownership is per test via <see cref="PostgreSqlTestDatabase"/>.
/// </summary>
public static class SharedPostgreSqlContainer
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static PostgreSqlContainer? container;
    private static Exception? startFailure;

    public static async Task<PostgreSqlContainer> GetOrStartAsync(
        CancellationToken cancellationToken = default)
    {
        if (container is not null)
        {
            return container;
        }

        if (startFailure is not null)
        {
            throw CreateStartFailure(startFailure);
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (container is not null)
            {
                return container;
            }

            if (startFailure is not null)
            {
                throw CreateStartFailure(startFailure);
            }

            PostgreSqlContainer candidate = new PostgreSqlBuilder(PostgreSqlTestImage.Reference)
                .WithDatabase("postgres")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            try
            {
                await candidate.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                startFailure = exception;
                await DisposeQuietlyAsync(candidate).ConfigureAwait(false);
                throw CreateStartFailure(exception);
            }

            container = candidate;
            return container;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static InvalidOperationException CreateStartFailure(Exception exception) =>
        new(
            $"Failed to start the PostgreSQL integration-test container using image '{PostgreSqlTestImage.Reference}'. " +
            "This is a hard test failure; provider tests do not fall back to InMemory or SQLite.",
            exception);

    private static async Task DisposeQuietlyAsync(PostgreSqlContainer candidate)
    {
        try
        {
            await candidate.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort dispose after a failed start; the original start failure is rethrown.
        }
    }
}
