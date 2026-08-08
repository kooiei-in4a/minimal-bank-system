using Npgsql;

namespace MinimalBankSystem.PostgresIntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class CleanupFailureTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task NonForcedCleanupFailsExplicitlyWhileAConnectionIsStillActive()
    {
        await using PostgresTestDatabase database = await fixture.CreateDatabaseAsync();

        NpgsqlConnection blockingConnection = new(database.ConnectionString);
        await blockingConnection.OpenAsync();

        try
        {
            PostgresException failure = await Assert.ThrowsAsync<PostgresException>(
                () => database.DropAsync(force: false));

            Assert.Equal("55006", failure.SqlState);
        }
        finally
        {
            await blockingConnection.CloseAsync();
            await blockingConnection.DisposeAsync();
        }
    }
}
