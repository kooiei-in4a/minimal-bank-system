using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(PostgreSqlIntegrationFixture.Name)]
[Trait(PostgreSqlTestCategories.TraitName, PostgreSqlTestCategories.Integration)]
public sealed class PostgreSqlIsolationTests
{
    private readonly SharedPostgreSqlContainer _container;

    public PostgreSqlIsolationTests(SharedPostgreSqlContainer container)
    {
        _container = container;
    }

    [Fact]
    public async Task SeparateDatabasesDoNotShareTablesOrRows()
    {
        PostgreSqlTestDatabase firstDatabase = await PostgreSqlTestDatabase.CreateAsync(_container);
        PostgreSqlTestDatabase secondDatabase = await PostgreSqlTestDatabase.CreateAsync(_container);

        try
        {
            await using (NpgsqlConnection firstConnection = new(firstDatabase.ConnectionString))
            {
                await firstConnection.OpenAsync();
                await using NpgsqlCommand createFirst = firstConnection.CreateCommand();
                createFirst.CommandText = "CREATE TABLE isolation_probe (value int NOT NULL); INSERT INTO isolation_probe VALUES (41);";
                await createFirst.ExecuteNonQueryAsync();
            }

            await using NpgsqlConnection secondConnection = new(secondDatabase.ConnectionString);
            await secondConnection.OpenAsync();
            await using NpgsqlCommand listTables = secondConnection.CreateCommand();
            listTables.CommandText =
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'isolation_probe'";
            long tableCount = (long)(await listTables.ExecuteScalarAsync())!;

            Assert.Equal(0, tableCount);
        }
        finally
        {
            await firstDatabase.DisposeAsync();
            await secondDatabase.DisposeAsync();
        }
    }

    [Fact]
    public async Task ParallelWritesToIsolatedDatabasesDoNotInterfere()
    {
        PostgreSqlTestDatabase firstDatabase = await PostgreSqlTestDatabase.CreateAsync(_container);
        PostgreSqlTestDatabase secondDatabase = await PostgreSqlTestDatabase.CreateAsync(_container);

        try
        {
            Task firstWrite = WriteValueAsync(firstDatabase.ConnectionString, 100);
            Task secondWrite = WriteValueAsync(secondDatabase.ConnectionString, 200);
            await Task.WhenAll(firstWrite, secondWrite);

            int firstValue = await ReadSingleValueAsync(firstDatabase.ConnectionString);
            int secondValue = await ReadSingleValueAsync(secondDatabase.ConnectionString);

            Assert.Equal(100, firstValue);
            Assert.Equal(200, secondValue);
        }
        finally
        {
            await firstDatabase.DisposeAsync();
            await secondDatabase.DisposeAsync();
        }
    }

    private static async Task WriteValueAsync(string connectionString, int value)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE parallel_probe (value int NOT NULL); INSERT INTO parallel_probe VALUES (@value);";
        command.Parameters.AddWithValue("value", value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadSingleValueAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM parallel_probe LIMIT 1";
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
