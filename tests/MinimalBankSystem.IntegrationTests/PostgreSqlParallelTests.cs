using Npgsql;

namespace MinimalBankSystem.IntegrationTests;

public sealed class PostgreSqlParallelTests(PostgreSqlTestFixture fixture)
    : IClassFixture<PostgreSqlTestFixture>
{
    [Fact]
    [Trait("Category", PostgreSqlTestFixture.Category)]
    public async Task IsolatedDatabasesCanBeUsedConcurrently()
    {
        await using PostgreSqlDatabaseLease first = await fixture.CreateDatabaseAsync();
        await using PostgreSqlDatabaseLease second = await fixture.CreateDatabaseAsync();

        string[] values = await Task.WhenAll(
            WriteAndReadAsync(first, "first"),
            WriteAndReadAsync(second, "second"));

        Assert.Equal(["first", "second"], values);
    }

    private static async Task<string> WriteAndReadAsync(
        PostgreSqlDatabaseLease database,
        string expectedValue)
    {
        await using NpgsqlConnection connection = new(database.ConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand createCommand = connection.CreateCommand();
        createCommand.CommandText = "CREATE TABLE test_parallel_state (value text NOT NULL);";
        await createCommand.ExecuteNonQueryAsync();

        await using NpgsqlCommand insertCommand = connection.CreateCommand();
        insertCommand.CommandText = "INSERT INTO test_parallel_state (value) VALUES ($1);";
        insertCommand.Parameters.AddWithValue(expectedValue);
        await insertCommand.ExecuteNonQueryAsync();

        await using NpgsqlCommand readCommand = connection.CreateCommand();
        readCommand.CommandText = "SELECT value FROM test_parallel_state;";
        return (string)(await readCommand.ExecuteScalarAsync())!;
    }
}
