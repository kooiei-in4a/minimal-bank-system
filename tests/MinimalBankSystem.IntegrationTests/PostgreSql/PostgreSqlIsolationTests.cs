namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(PostgreSqlTestCollections.Name)]
[Trait(PostgreSqlTestCollections.CategoryTraitName, PostgreSqlTestCollections.CategoryTraitValue)]
public sealed class PostgreSqlIsolationTests(
    PostgreSqlTestDatabase firstDatabase,
    SecondPostgreSqlTestDatabase secondDatabase)
    : IClassFixture<PostgreSqlTestDatabase>, IClassFixture<SecondPostgreSqlTestDatabase>
{
    [Fact]
    public async Task ClassDatabasesAreMutuallyIsolated()
    {
        const string table = "isolation_probe";

        await PostgreSqlTestSql.ExecuteNonQueryAsync(
            firstDatabase.ConnectionString,
            $"CREATE TABLE {table} (value text)");
        await PostgreSqlTestSql.ExecuteNonQueryAsync(
            firstDatabase.ConnectionString,
            $"INSERT INTO {table} (value) VALUES ('from-first')");

        object? tableCountInSecond = await PostgreSqlTestSql.ExecuteScalarAsync(
            secondDatabase.ConnectionString,
            $"SELECT count(*) FROM information_schema.tables WHERE table_name = '{table}'");
        Assert.Equal(0L, tableCountInSecond);

        object? valueInFirst = await PostgreSqlTestSql.ExecuteScalarAsync(
            firstDatabase.ConnectionString,
            $"SELECT value FROM {table}");
        Assert.Equal("from-first", valueInFirst);

        await PostgreSqlTestSql.ExecuteNonQueryAsync(
            secondDatabase.ConnectionString,
            $"CREATE TABLE {table} (value text)");
        await PostgreSqlTestSql.ExecuteNonQueryAsync(
            secondDatabase.ConnectionString,
            $"INSERT INTO {table} (value) VALUES ('from-second')");

        object? valueInSecond = await PostgreSqlTestSql.ExecuteScalarAsync(
            secondDatabase.ConnectionString,
            $"SELECT value FROM {table}");
        Assert.Equal("from-second", valueInSecond);

        object? rowCountInFirst = await PostgreSqlTestSql.ExecuteScalarAsync(
            firstDatabase.ConnectionString,
            $"SELECT count(*) FROM {table}");
        Assert.Equal(1L, rowCountInFirst);
    }
}

public sealed class SecondPostgreSqlTestDatabase(PostgreSqlContainerFixture container)
    : PostgreSqlTestDatabase(container, "IsolationSecond");
