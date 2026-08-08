using Npgsql;

namespace MinimalBankSystem.IntegrationTests;

public sealed class PostgreSqlIsolationTests(PostgreSqlTestFixture fixture)
    : IClassFixture<PostgreSqlTestFixture>
{
    [Fact]
    [Trait("Category", PostgreSqlTestFixture.Category)]
    public async Task DatabasesCreatedForDifferentTestsDoNotShareState()
    {
        await using PostgreSqlDatabaseLease first = await fixture.CreateDatabaseAsync();
        await using PostgreSqlDatabaseLease second = await fixture.CreateDatabaseAsync();

        await using NpgsqlConnection firstConnection = new(first.ConnectionString);
        await firstConnection.OpenAsync();
        await using NpgsqlCommand createCommand = firstConnection.CreateCommand();
        createCommand.CommandText = "CREATE TABLE test_state (value text NOT NULL);";
        await createCommand.ExecuteNonQueryAsync();

        await using NpgsqlCommand insertCommand = firstConnection.CreateCommand();
        insertCommand.CommandText = "INSERT INTO test_state (value) VALUES ('first');";
        await insertCommand.ExecuteNonQueryAsync();

        await using NpgsqlConnection secondConnection = new(second.ConnectionString);
        await secondConnection.OpenAsync();
        await using NpgsqlCommand lookupCommand = secondConnection.CreateCommand();
        lookupCommand.CommandText = "SELECT to_regclass('public.test_state')::text;";

        Assert.Null((await lookupCommand.ExecuteScalarAsync()) as string);
    }
}
