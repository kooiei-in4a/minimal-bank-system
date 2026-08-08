using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(PostgreSqlTestCollections.Name)]
[Trait(PostgreSqlTestCollections.CategoryTraitName, PostgreSqlTestCollections.CategoryTraitValue)]
public sealed class PostgreSqlTestDatabaseLifecycleTests(PostgreSqlContainerFixture container, PostgreSqlTestDatabase database)
    : IClassFixture<PostgreSqlTestDatabase>
{
    [Fact]
    public async Task ClassDatabaseIsCreatedBeforeTestsAndIsUsable()
    {
        Assert.True(await PostgreSqlTestDatabase.ExistsAsync(container, database.DatabaseName));

        const string table = "lifecycle_probe";
        await PostgreSqlTestSql.ExecuteNonQueryAsync(
            database.ConnectionString,
            $"CREATE TABLE {table} (value text)");
        await PostgreSqlTestSql.ExecuteNonQueryAsync(
            database.ConnectionString,
            $"INSERT INTO {table} (value) VALUES ('roundtrip')");
        object? count = await PostgreSqlTestSql.ExecuteScalarAsync(
            database.ConnectionString,
            $"SELECT count(*) FROM {table}");

        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task ScratchDatabaseCanBeCreatedAndDropped()
    {
        string databaseName = PostgreSqlTestDatabase.CreateDatabaseName("ScratchProbe");

        Assert.False(await PostgreSqlTestDatabase.ExistsAsync(container, databaseName));

        await PostgreSqlTestDatabase.CreateAsync(container, databaseName);
        Assert.True(await PostgreSqlTestDatabase.ExistsAsync(container, databaseName));

        await PostgreSqlTestDatabase.DropAsync(container, databaseName);
        Assert.False(await PostgreSqlTestDatabase.ExistsAsync(container, databaseName));
    }

    [Fact]
    public async Task ConnectionFailureIsReportedAsAnException()
    {
        await container.EnsureStartedAsync();

        NpgsqlConnectionStringBuilder builder = new(container.AdminConnectionString)
        {
            Password = "wrong-password",
        };

        NpgsqlException failure = await Assert.ThrowsAnyAsync<NpgsqlException>(
            () => PostgreSqlTestSql.ExecuteScalarAsync(
                builder.ConnectionString,
                "SELECT 1"));

        Assert.Equal("28P01", failure.SqlState);
    }
}
