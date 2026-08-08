using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSql")]
public sealed class PostgreSqlParallelExecutionTests(PostgreSqlContainerFixture fixture)
{
    [Fact]
    public async Task MultipleTestsCanCreateDatabasesConcurrently()
    {
        const int concurrentCount = 5;

        Task<PostgreSqlTestDatabase>[] createTasks =
            Enumerable.Range(0, concurrentCount)
                .Select(_ => PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString))
                .ToArray();

        PostgreSqlTestDatabase[] databases = await Task.WhenAll(createTasks);

        HashSet<string> databaseNames = databases.Select(d => d.DatabaseName).ToHashSet();
        Assert.Equal(concurrentCount, databaseNames.Count);

        foreach (PostgreSqlTestDatabase db in databases)
        {
            await using NpgsqlConnection connection = await db.OpenConnectionAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE concurrent_test (id integer PRIMARY KEY, value text)";
            await command.ExecuteNonQueryAsync();

            await using NpgsqlCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO concurrent_test (id, value) VALUES (1, @val)";
            insert.Parameters.AddWithValue("val", db.DatabaseName);
            await insert.ExecuteNonQueryAsync();
        }

        foreach (PostgreSqlTestDatabase db in databases)
        {
            await using NpgsqlConnection connection = await db.OpenConnectionAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM concurrent_test WHERE id = 1";
            object? result = await command.ExecuteScalarAsync();

            Assert.Equal(db.DatabaseName, result);
        }

        foreach (PostgreSqlTestDatabase db in databases)
        {
            await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task DatabaseIsolationPreventsCrossTestInterference()
    {
        await using PostgreSqlTestDatabase dbA = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);
        await using PostgreSqlTestDatabase dbB = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);

        await using (NpgsqlConnection connA = await dbA.OpenConnectionAsync())
        {
            await using NpgsqlCommand createTable = connA.CreateCommand();
            createTable.CommandText = "CREATE TABLE test_data (id serial PRIMARY KEY, data text)";
            await createTable.ExecuteNonQueryAsync();

            await using NpgsqlCommand insert = connA.CreateCommand();
            insert.CommandText = "INSERT INTO test_data (data) VALUES ('from A')";
            await insert.ExecuteNonQueryAsync();
        }

        await using (NpgsqlConnection connB = await dbB.OpenConnectionAsync())
        {
            await using NpgsqlCommand checkTable = connB.CreateCommand();
            checkTable.CommandText =
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'test_data')";
            object? tableExists = await checkTable.ExecuteScalarAsync();
            Assert.False((bool)tableExists!);

            await using NpgsqlCommand createOwnTable = connB.CreateCommand();
            createOwnTable.CommandText = "CREATE TABLE test_data (id serial PRIMARY KEY, data text)";
            await createOwnTable.ExecuteNonQueryAsync();

            await using NpgsqlCommand insert = connB.CreateCommand();
            insert.CommandText = "INSERT INTO test_data (data) VALUES ('from B')";
            await insert.ExecuteNonQueryAsync();
        }

        await using (NpgsqlConnection connA = await dbA.OpenConnectionAsync())
        {
            await using NpgsqlCommand query = connA.CreateCommand();
            query.CommandText = "SELECT data FROM test_data WHERE id = 1";
            object? result = await query.ExecuteScalarAsync();
            Assert.Equal("from A", result);
        }

        await using (NpgsqlConnection connB = await dbB.OpenConnectionAsync())
        {
            await using NpgsqlCommand query = connB.CreateCommand();
            query.CommandText = "SELECT data FROM test_data WHERE id = 1";
            object? result = await query.ExecuteScalarAsync();
            Assert.Equal("from B", result);
        }
    }
}
