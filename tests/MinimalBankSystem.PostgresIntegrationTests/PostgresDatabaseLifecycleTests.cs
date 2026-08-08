using MinimalBankSystem.PostgresIntegrationTests.Fixtures;

namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// Proves that the database lifecycle is automatic and safe to drive concurrently.
/// </summary>
[Trait(PostgresTestCategories.Category, PostgresTestCategories.PostgresIntegration)]
public sealed class PostgresDatabaseLifecycleTests
{
    [Fact]
    public async Task DisposingADatabaseDropsIt()
    {
        PostgresTestServer server = await PostgresTestServer.SharedAsync();
        PostgresTestDatabase database = await server.CreateDatabaseAsync(nameof(DisposingADatabaseDropsIt));

        Assert.True(await server.DatabaseExistsAsync(database.Name));

        await database.DisposeAsync();

        Assert.False(await server.DatabaseExistsAsync(database.Name));
    }

    [Fact]
    public async Task DisposingTwiceIsNotAnError()
    {
        PostgresTestServer server = await PostgresTestServer.SharedAsync();
        PostgresTestDatabase database = await server.CreateDatabaseAsync(nameof(DisposingTwiceIsNotAnError));

        await database.DisposeAsync();
        await database.DisposeAsync();

        Assert.False(await server.DatabaseExistsAsync(database.Name));
    }

    [Fact]
    public async Task ConcurrentCreationProducesDistinctDatabases()
    {
        const int concurrency = 8;
        PostgresTestServer server = await PostgresTestServer.SharedAsync();

        PostgresTestDatabase[] databases = await Task.WhenAll(
            Enumerable
                .Range(0, concurrency)
                .Select(index => server.CreateDatabaseAsync($"concurrent{index}")));

        try
        {
            Assert.Equal(
                concurrency,
                databases.Select(database => database.Name).Distinct(StringComparer.Ordinal).Count());

            bool[] existence = await Task.WhenAll(
                databases.Select(database => server.DatabaseExistsAsync(database.Name)));
            Assert.All(existence, Assert.True);
        }
        finally
        {
            foreach (PostgresTestDatabase database in databases)
            {
                await database.DisposeAsync();
            }
        }

        bool[] remaining = await Task.WhenAll(
            databases.Select(database => server.DatabaseExistsAsync(database.Name)));
        Assert.All(remaining, Assert.False);
    }
}
