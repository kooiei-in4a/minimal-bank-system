using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(PostgreSqlTestCollections.Name)]
[Trait(PostgreSqlTestCollections.CategoryTraitName, PostgreSqlTestCollections.CategoryTraitValue)]
public sealed class PostgreSqlCleanupFailureTests(PostgreSqlContainerFixture container)
{
    [Fact]
    public async Task DatabaseDropFailureIsSurfacedAndNotSilentlyIgnored()
    {
        string databaseName = PostgreSqlTestDatabase.CreateDatabaseName("DropFailureProbe");
        await PostgreSqlTestDatabase.CreateAsync(container, databaseName);

        await using NpgsqlConnection blockingConnection = await PostgreSqlTestSql.OpenConnectionAsync(
            container.GetDatabaseConnectionString(databaseName));

        NpgsqlException dropFailure = await Assert.ThrowsAnyAsync<NpgsqlException>(
            () => PostgreSqlTestDatabase.DropAsync(container, databaseName));

        Assert.Equal("55006", dropFailure.SqlState);

        await blockingConnection.CloseAsync();

        await PostgreSqlTestDatabase.DropAsync(container, databaseName);
        Assert.False(await PostgreSqlTestDatabase.ExistsAsync(container, databaseName));
    }
}
