using Npgsql;

namespace MinimalBankSystem.PostgresIntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class DatabaseIsolationTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task TwoDatabasesDoNotShareTablesOrRows()
    {
        await using PostgresTestDatabase first = await fixture.CreateDatabaseAsync();
        await using PostgresTestDatabase second = await fixture.CreateDatabaseAsync();

        await CreateProbeTableAsync(first.ConnectionString);
        await InsertProbeRowAsync(first.ConnectionString, "marker-in-first-database");

        Assert.False(await ProbeTableExistsAsync(second.ConnectionString));

        await CreateProbeTableAsync(second.ConnectionString);
        Assert.Empty(await ReadProbeValuesAsync(second.ConnectionString));

        string[] firstValues = await ReadProbeValuesAsync(first.ConnectionString);
        Assert.Equal(["marker-in-first-database"], firstValues);
    }

    [Fact]
    public async Task ManyDatabasesCanBeCreatedWrittenToAndDroppedConcurrentlyWithoutInterference()
    {
        const int concurrentDatabases = 8;

        IEnumerable<Task<string[]>> operations = Enumerable
            .Range(0, concurrentDatabases)
            .Select(RunIsolatedProbeRoundTripAsync);

        string[][] results = await Task.WhenAll(operations);

        for (int i = 0; i < concurrentDatabases; i++)
        {
            Assert.Equal([$"value-from-worker-{i}"], results[i]);
        }
    }

    private async Task<string[]> RunIsolatedProbeRoundTripAsync(int workerIndex)
    {
        await using PostgresTestDatabase database = await fixture.CreateDatabaseAsync();
        await CreateProbeTableAsync(database.ConnectionString);
        await InsertProbeRowAsync(database.ConnectionString, $"value-from-worker-{workerIndex}");
        return await ReadProbeValuesAsync(database.ConnectionString);
    }

    private static async Task CreateProbeTableAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = new(
            "CREATE TABLE fixture_probe (id SERIAL PRIMARY KEY, value TEXT NOT NULL)",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertProbeRowAsync(string connectionString, string value)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = new("INSERT INTO fixture_probe (value) VALUES (@value)", connection);
        command.Parameters.AddWithValue("value", value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string[]> ReadProbeValuesAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = new("SELECT value FROM fixture_probe ORDER BY id", connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> values = [];
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }

    private static async Task<bool> ProbeTableExistsAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'fixture_probe')",
            connection);

        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
