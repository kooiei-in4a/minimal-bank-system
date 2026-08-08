using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection("PostgreSql")]
public sealed class PostgreSqlIsolationTests(PostgreSqlContainerFixture fixture)
{
    [Fact]
    public async Task TwoTestsUseSeparateDatabases()
    {
        await using PostgreSqlTestDatabase dbA = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);
        await using PostgreSqlTestDatabase dbB = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);

        Assert.NotEqual(dbA.DatabaseName, dbB.DatabaseName);
        Assert.NotEqual(dbA.TestConnectionString, dbB.TestConnectionString);
    }

    [Fact]
    public async Task TableInOneDatabaseIsNotVisibleInAnother()
    {
        await using PostgreSqlTestDatabase dbA = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);
        await using PostgreSqlTestDatabase dbB = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);

        await using (NpgsqlConnection connA = await dbA.OpenConnectionAsync())
        {
            await using NpgsqlCommand createTable = connA.CreateCommand();
            createTable.CommandText = "CREATE TABLE probe_test (id integer PRIMARY KEY)";
            await createTable.ExecuteNonQueryAsync();

            await using NpgsqlCommand insert = connA.CreateCommand();
            insert.CommandText = "INSERT INTO probe_test (id) VALUES (1)";
            await insert.ExecuteNonQueryAsync();
        }

        await using (NpgsqlConnection connB = await dbB.OpenConnectionAsync())
        {
            await using NpgsqlCommand checkTable = connB.CreateCommand();
            checkTable.CommandText =
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'probe_test')";
            object? exists = await checkTable.ExecuteScalarAsync();

            Assert.False((bool)exists!);
        }
    }

    [Fact]
    public async Task DatabaseIsCleanedUpAfterDispose()
    {
        string databaseName;

        {
            await using PostgreSqlTestDatabase db = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);
            databaseName = db.DatabaseName;
        }

        await using NpgsqlConnection masterConnection = new(fixture.ConnectionString);
        await masterConnection.OpenAsync();

        await using NpgsqlCommand checkCmd = masterConnection.CreateCommand();
        checkCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)";
        checkCmd.Parameters.AddWithValue("name", databaseName);
        object? exists = await checkCmd.ExecuteScalarAsync();

        Assert.False((bool)exists!);
    }

    [Fact]
    public async Task DisposedDatabaseConnectionIsRejected()
    {
        string connectionString;

        {
            await using PostgreSqlTestDatabase db = await PostgreSqlTestDatabase.CreateAsync(fixture.ConnectionString);
            connectionString = db.TestConnectionString;
        }

        await using NpgsqlConnection connection = new(connectionString);
        await Assert.ThrowsAnyAsync<Exception>(async () => await connection.OpenAsync());
    }
}
