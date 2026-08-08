using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(PostgreSqlIntegrationFixture.Name)]
[Trait(PostgreSqlTestCategories.TraitName, PostgreSqlTestCategories.Integration)]
public sealed class PostgreSqlCleanupFailureTests
{
    private readonly SharedPostgreSqlContainer _container;

    public PostgreSqlCleanupFailureTests(SharedPostgreSqlContainer container)
    {
        _container = container;
    }

    [Fact]
    public async Task CleanupFailureIsNotSilentlyIgnored()
    {
        PostgreSqlTestDatabase database = await PostgreSqlTestDatabase.CreateAsync(_container);

        await using NpgsqlConnection blocker = new(database.ConnectionString);
        await blocker.OpenAsync();

        Exception? cleanupException = await Record.ExceptionAsync(
            async () => await database.DisposeAsync(terminateBackends: false));

        Assert.NotNull(cleanupException);
        Assert.IsAssignableFrom<PostgreSqlTestCleanupException>(cleanupException);

        await blocker.CloseAsync();
        await database.DisposeAsync();
    }
}
