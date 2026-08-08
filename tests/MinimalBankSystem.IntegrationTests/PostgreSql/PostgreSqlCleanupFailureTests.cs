using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSql")]
public sealed class PostgreSqlCleanupFailureTests(PostgreSqlContainerFixture fixture)
{
    [Fact]
    public async Task DisposeTerminatesOpenConnectionsAndDropsDatabase()
    {
        PostgreSqlTestDatabase db = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);
        string databaseName = db.DatabaseName;

        NpgsqlConnection leakedConnection = await db.OpenConnectionAsync();

        await db.DisposeAsync();

        await using NpgsqlCommand cmd = leakedConnection.CreateCommand();
        cmd.CommandText = "SELECT 1";
        await Assert.ThrowsAnyAsync<NpgsqlException>(async () => await cmd.ExecuteScalarAsync());

        await using NpgsqlConnection masterConnection = new(fixture.ConnectionString);
        await masterConnection.OpenAsync();

        await using NpgsqlCommand checkCmd = masterConnection.CreateCommand();
        checkCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)";
        checkCmd.Parameters.AddWithValue("name", databaseName);
        object? exists = await checkCmd.ExecuteScalarAsync();

        Assert.False((bool)exists!);
    }

    [Fact]
    public async Task DoubleDisposeDoesNotThrow()
    {
        PostgreSqlTestDatabase db = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);

        await db.DisposeAsync();
        await db.DisposeAsync();
    }
}
